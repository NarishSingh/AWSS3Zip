using AWSS3Zip.Commands.Contracts;
using AWSS3Zip.Entity;
using AWSS3Zip.Entity.Models;
using AWSS3Zip.Models;
using AWSS3Zip.Service;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AWSS3Zip.Commands;

public class ExtractJob : IProcessJob
{
    public string[] Parameters { get; set; } = default!;
    public string TempDir { get; set; } = default!;
    public string ConnectionString { get; set; } = default!;

    private readonly string _zipper = $"{AppContext.BaseDirectory}7-Zip\\7z.exe";

    public ExtractJob BuildParameters(string[] parameters)
    {
        Parameters = parameters;

        return this;
    }

    /// <summary>
    /// NOTE: This was the only implementation of execute
    /// </summary>
    public void Execute()
    {
        #region ARG PARSING
        int iPath = Array.IndexOf(Parameters, "-e");
        int dbPosition = Array.IndexOf(Parameters, "-db") + Array.IndexOf(Parameters, "--database") + 1;

        bool isDbTask = dbPosition >= 0;

        if (isDbTask && Parameters.Length > dbPosition + 1 && Parameters[dbPosition + 1].Contains("Server="))
            ConnectionString = Parameters[dbPosition + 1];

        if (iPath == -1)
            Array.IndexOf(Parameters, "--extract");

        if (iPath != -1 && (Parameters[iPath + 1].Contains('-') || Parameters[iPath + 1].Contains("--")))
        {
            Console.WriteLine("Command not formatted Correctly, contains '-' or '--' followed by command variable");
            return;
        }

        int iOutput = Array.IndexOf(Parameters, "-o");
        if (iOutput == -1)
            Array.IndexOf(Parameters, "--output");

        if (iOutput != -1 && (Parameters[iOutput + 1].Contains('-') || Parameters[iOutput + 1].Contains("--")))
        {
            Console.WriteLine("Command not formatted Correctly, contains '-' or '--' followed by command variable");
            return;
        }
        #endregion

        if (iPath != -1)
            PrepareAndExecute(iPath, isDbTask, iOutput);
        else
            Console.WriteLine("No execution command found!");
    }

    private void PrepareAndExecute(int iPath, bool isDbTask, int iOutput = -1)
    {
        try
        {
            TempDir = $"{AppContext.BaseDirectory}output";
            string? outputDirectory = iOutput != -1 ? Parameters[iOutput + 1] : null;

            string extractCmdArgs = $@"x {Parameters[iPath + 1]} -o{TempDir}";

            if (Directory.GetFileSystemEntries(TempDir).Length == 0)
            {
                Processor.InvokeProcess(_zipper, extractCmdArgs);
                Console.WriteLine($"\n Files Extracted: {TempDir}");
            }
            else
            {
                Console.WriteLine("Temp dir is not empty. Skipping extract...");
            }

            Console.WriteLine("\n Creating Database and building directory structure...");

            // DEFINE TABLE
            using DatabaseContext? context = new();

            context.Build(ConnectionString).Database.EnsureCreated();
            context.Database.SaveChanges();

            try
            {
                int result = context.Database.Database.ExecuteSql($"EXEC [dbo].[sp_CreateTbl];");
                if (result == -1)
                    Console.WriteLine("Table IISLogEvents already exists in destination db");
                else
                    Console.WriteLine($"Rows effected by attempted IISLogEvents table creation to output db: {result}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"SQL Query Failed - {e.Message}\n\n");
            }

            // EXTRACT
            DirectoryNode root = ExecuteJob(TempDir, isDbTask);

            Console.WriteLine($"Finishing Job...Deleting temp dir... {root.Path}");
            if (Directory.Exists(root.Path))
                Directory.Delete(root.Path);
            else if (File.Exists(root.Path))
                File.Delete(root.Path);

            if (outputDirectory != null)
                File.Move($"{AppContext.BaseDirectory}localdb", outputDirectory);
        }
        catch (Exception e)
        {
            Console.WriteLine($"zip path file not found error!\nDetails:\n\t{e.Message}");
        }
    }

    private DirectoryNode ExecuteJob(string directory, bool isDbTask, DirectoryNode node = default!, bool isHead = true)
    {
        node ??= new DirectoryNode();

        if (Directory.Exists(directory))
        {
            #region MAP THE DIR TREE
            string[] dirs = Directory.GetDirectories(directory);

            if (dirs.Length > 0)
            {
                foreach (string dirPath in dirs)
                {
                    if (isHead)
                    {
                        node.Name = GetFileName(dirPath);
                        node.Path = dirPath;
                        node.Type = FileType.DIR;

                        isHead = false;
                        node.Inside = new DirectoryNode
                        {
                            Parent = node
                        };
                    }
                    else
                    {
                        node.Next = new DirectoryNode(GetFileName(dirPath), dirPath)
                        {
                            Previous = node,
                            Parent = node.Parent
                        };
                        node = node.Next;
                        node.Inside = new DirectoryNode
                        {
                            Parent = node
                        };
                    }
                }
            }
            else
            {
                foreach (string filePath in Directory.GetFiles(directory))
                {
                    if (isHead)
                    {
                        node.Name = GetFileName(filePath);
                        node.Path = filePath;
                        node.Type = node.Name.Contains('~') ? FileType.TXT : FileType.ZIP;

                        isHead = false;
                    }
                    else
                    {
                        string name = GetFileName(filePath);
                        node.Next = new DirectoryNode(name, filePath)
                        {
                            Previous = node,
                            Parent = node.Parent,
                            Type = name.Contains('~') ? FileType.TXT : FileType.ZIP
                        };

                        node = node.Next;
                        node.Inside = new DirectoryNode
                        {
                            Parent = node
                        };
                    }
                }
            }

            isHead = true;
            #endregion

            return RecurseLogEvents(directory, node, isHead, isDbTask);
        }

        if (node.Previous != null)
            return ExecuteJob(node.Previous.Path, isDbTask, node.Previous, isHead);
        else if (node.Parent != null)
            return ExecuteJob(node.Parent.Path, isDbTask, node.Parent, isHead);
        else
            return node;
    }

    private DirectoryNode RecurseLogEvents(string currentDir, DirectoryNode node, bool isHead, bool isDbTask,
        Func<DirectoryNode, bool> cleanupNode = null, bool isParent = false)
    {
        if (cleanupNode != null)
        {
            if (isParent && node.Inside != null)
            {
                if (node.Inside.Type == FileType.DIR)
                    Directory.Delete(node.Inside.Path);
                else
                    File.Delete(node.Inside.Path);
            }
            else if (!isParent)
            {
                cleanupNode(node);

                if (node.Previous == null)
                    RecurseLogEvents(currentDir, node, isHead, isDbTask);
            }
        }

        string? previousDirectory = currentDir;
        currentDir = node.Name != null && !currentDir.Contains(node.Name)
            ? $"{currentDir}\\{node.Name}"
            : currentDir;

        if (Directory.Exists(currentDir))
        {
            if (Directory.GetFileSystemEntries(currentDir).Length == 0)
            {
                if (node.Previous != null && node.Path.Equals(currentDir))
                {
                    node = node.Previous;
                    Cleanup(ref node);
                }
                else if (node.Previous != null && !node.Path.Equals(currentDir))
                {
                    Directory.Delete(currentDir);
                    currentDir = node.Path;
                }
                else if (node.Parent != null)
                {
                    node = node.Parent;
                    Cleanup(ref node, true);
                }
                else if (node.Parent == null)
                {
                    node.Path = currentDir;

                    return node;
                }
            }

            return ExecuteJob(currentDir, isDbTask, node.Inside, isHead);
        }
        else
        {
            // Extract if current item is a zip
            if (node.Type.Equals(FileType.ZIP))
            {
                Console.WriteLine("Unzipping contents of inner zip files...May take a while... ");
                string extractCmdArgs = $@"x {currentDir} -o{previousDirectory}";
                Processor.InvokeProcess(_zipper, extractCmdArgs);

                Console.WriteLine("Deleting previous zip file.. ");
                File.Delete(currentDir);
                node.Name += node.Name.Contains('~') ? string.Empty : '~';
                node.Path += node.Path.Contains('~') ? string.Empty : '~';
                node.Type = FileType.TXT;
            }

            if (node.Type.Equals(FileType.TXT))
            {
                // FIXME find an example of aws log

                #region DESERIALIZATION
                string json = File.ReadAllText(node.Path);

                // normalize to JSON structure?
                json = json.Insert(0, "[") + "]";
                json = json.Replace("}{", "},{");

                /**
                 * 1 file => List of `IISLog`
                 * -> Each log record contains a list of `LogEvent` which will contain the request
                 */
                List<IISLogEvent> entities = [];
                foreach (IISLog log in JsonSerializer.Deserialize<List<IISLog>>(json))
                {
                    // flatten the record to create output entity
                    entities.AddRange(log.logEvents.Select(s => new IISLogEvent
                    {
                        Id = s.id,
                        MessageType = log.messageType,
                        Owner = log.owner,
                        LogGroup = log.logGroup,
                        LogStream = log.logStream,
                        SubscriptionFilters = JsonSerializer.Serialize(log.subscriptionFilters),
                        DateTime = DateTimeOffset.FromUnixTimeMilliseconds(s.timestamp).DateTime,
                        RequestMessage = s.message
                    }));
                }
                #endregion

                if (isDbTask) // FIXME should remove...just let it go to db
                {
                    using (AppDatabase? context = new DatabaseContext().Build(ConnectionString))
                    {
                        try
                        {
                            context.IISLogEvents.AddRange(entities);
                            context.SaveChanges();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Caught Error:\n{e.Message}\n Retrying by detatching Entities and saving individually modified...");
                            context.AttachSaveEntities(entities);
                        }
                    }

                    entities.Clear();

                    Console.WriteLine("Changes Saved to destination DB!");
                }
            }

            node.Inside = null;
            string[] parts = previousDirectory.Split("\\");
            currentDir = string.Join("\\", parts, 0, parts.Length - 1);

            if (node.Previous != null)
                return RecurseLogEvents(previousDirectory, node.Previous, isHead, isDbTask, x => Cleanup(ref x));
            else if (!currentDir.Equals(TempDir) && node.Parent != null)
                return RecurseLogEvents(currentDir, node.Parent, isHead, isDbTask, x => Cleanup(ref x), true);
            else
                return node;
        }
    }

    private static string GetFileName(string path) => path.Split("\\").Last();

    private static bool Cleanup(ref DirectoryNode node, bool isParent = false)
    {
        if (!isParent)
        {
            if (node.Next.Type == FileType.DIR)
                Directory.Delete(node.Next.Path);
            else if (node.Next.Type == FileType.TXT)
                File.Delete(node.Next.Path);

            node.Next = null;
        }
        else
        {
            if (node.Inside.Type == FileType.DIR && node.Inside.Path != null)
                Directory.Delete(node.Inside.Path);
            else if (node.Inside.Type == FileType.TXT)
                File.Delete(node.Inside.Path);

            node.Inside = null;
        }

        return true;
    }
}
