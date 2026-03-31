using AWSS3Zip.Commands.Contracts;
using AWSS3Zip.Entity;
using AWSS3Zip.Entity.Models;
using AWSS3Zip.Models;
using AWSS3Zip.Service;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AWSS3Zip.Commands;

public class ExtractJob : IProcessJob
{
    public string[] Parameters { get; set; } = default!;
    public string TempDir { get; set; } = default!;
    public string ConnectionString { get; set; } = default!;

    private bool _isDatabaseTask;
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

        _isDatabaseTask = dbPosition >= 0;

        if (_isDatabaseTask && Parameters.Length > dbPosition + 1 && Parameters[dbPosition + 1].Contains("Server="))
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
            ExtractZipFilesAndProcess(iPath, iOutput);
        else
            Console.WriteLine("no execution command found!");
    }

    private void ExtractZipFilesAndProcess(int iPath, int iOutput = -1)
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

            // DEFINE TABLE IF OUTPUT TO DB
            if (_isDatabaseTask)
            {
                using DatabaseContext? context = new();

                context.Build(ConnectionString).Database.EnsureCreated();
                context.Database.SaveChanges();

                if (context.Type == DB.Microsoft)
                {
                    const string sql = """
                        CREATE TABLE IISLogEvents (
                            RowId INT Identity(1,1) PRIMARY KEY,
                            Id NVARCHAR(100) NULL,
                            MessageType NVARCHAR(100) NULL,
                            "Owner" NVARCHAR(50) NULL,
                            LogGroup NVARCHAR(50) NULL,
                            LogStream NVARCHAR(50) NULL,
                            SubscriptionFilters NVARCHAR(50) NULL,
                            "DateTime" DATETIME NULL,
                            RequestMessage NVARCHAR(MAX) NULL
                        );
                    """;

                    Console.WriteLine("You may need to manually create the IISLogEvents table in the database..\n" +
                        "Entity Framework Cannot guarantee code first table creation on your database schema programmatically\n" +
                        "Attempting to run Create Script Query -- requires your account to have sufficient privilege through connections string\n\n"
                    );

                    try
                    {
                        //context.Database.Database.ExecuteSqlRaw(sql); // FIXME turn this into a sql cmd type
                        context.Database.Database.ExecuteSql(FormattableStringFactory.Create(sql));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Execute raw sql failed with message.. {e.Message}\n\n Table may exist... Continuing process...");
                    }
                }
            }

            // EXTRACT
            DirectoryNode root = ProcessJob(TempDir);
            Console.WriteLine($"Deleting Root Directory... Finishing Job {root.Path}");
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

    /// <summary>
    /// Recursive job loop
    /// </summary>
    /// <param name="directory">Current directory level to traverse</param>
    /// <param name="node">Current item node being recursively operated on</param>
    /// <param name="head">boolean for indicating if `node` is the head of tree or not</param>
    /// <returns></returns>
    private DirectoryNode ProcessJob(string directory, DirectoryNode node = default!, bool head = true)
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
                    if (head)
                    {
                        node.Name = GetFileName(dirPath);
                        node.Path = dirPath;
                        node.Type = FileType.DIR;

                        head = false;
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
                    if (head)
                    {
                        node.Name = GetFileName(filePath);
                        node.Path = filePath;
                        node.Type = node.Name.Contains('~') ? FileType.TXT : FileType.ZIP;

                        head = false;
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

            head = true;
            #endregion

            return Unzip_File_Execute_SQL_Task_And_Recurse_Directory(directory, node, head);
        }

        if (node.Previous != null)
            return ProcessJob(node.Previous.Path, node.Previous, head);
        else if (node.Parent != null)
            return ProcessJob(node.Parent.Path, node.Parent, head);
        else
            return node;
    }

    /// <summary>
    /// Extract file name from its full path
    /// </summary>
    /// <param name="path">Full path for file</param>
    /// <returns>Returns file name with extension</returns>
    private static string GetFileName(string path) => path.Split("\\").Last();

    private DirectoryNode Unzip_File_Execute_SQL_Task_And_Recurse_Directory(string currentDir, DirectoryNode node, bool first, Func<DirectoryNode, bool> cleanupNode = null, bool isParent = false)
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
                    Unzip_File_Execute_SQL_Task_And_Recurse_Directory(currentDir, node, first);
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

            return ProcessJob(currentDir, node.Inside, first);
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
                 * 1 file =>
                 * List of `IISLog` -> 
                 * Each log record contains a list of `LogEvent` which will contain the request
                 * 
                 * IISLogEvent -> view model entity
                 */
                List<IISLog>? logEventList = JsonSerializer.Deserialize<List<IISLog>>(json);
                List<IISLogEvent> entities = [];

                // FIXME null safety
                logEventList.ForEach(iisLog => entities.AddRange(iisLog.logEvents.Select(s => new IISLogEvent
                {
                    Id = s.id,
                    MessageType = iisLog.messageType,
                    Owner = iisLog.owner,
                    LogGroup = iisLog.logGroup,
                    LogStream = iisLog.logStream,
                    SubscriptionFilters = JsonSerializer.Serialize(iisLog.subscriptionFilters),
                    DateTime = DateTimeOffset.FromUnixTimeMilliseconds(s.timestamp).DateTime,
                    RequestMessage = s.message
                })));
                #endregion

                if (_isDatabaseTask)
                {
                    using (AppDatabase? context = new DatabaseContext().Build(ConnectionString))
                    {
                        try
                        {
                            context.IISLogEvents.AddRange(entities);
                            context.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Caught Error:\n{ex.Message}\n Retrying by Detatching Entities and saving individually modified...");
                            context.AttachSaveEntities(entities);
                        }
                    }

                    entities = [];

                    Console.WriteLine("Changes Saved to SQLite DB! \nYou can use Query Syntax -SQL to query data\nYou can take the local.db file and upload into SQLite db browser or MS Access");
                }
            }

            node.Inside = null;
            string[] parts = previousDirectory.Split("\\");
            currentDir = string.Join("\\", parts, 0, parts.Length - 1);

            if (node.Previous != null)
                return Unzip_File_Execute_SQL_Task_And_Recurse_Directory(previousDirectory, node.Previous, first, x => Cleanup(ref x));
            else if (!currentDir.Equals(TempDir) && node.Parent != null)
                return Unzip_File_Execute_SQL_Task_And_Recurse_Directory(currentDir, node.Parent, first, (x) => Cleanup(ref x), true);
            else
                return node;
        }
    }

    private bool Cleanup(ref DirectoryNode node, bool isParent = false)
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
