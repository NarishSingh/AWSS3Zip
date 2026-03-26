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
    public string OriginalDirectory { get; set; } = default!;
    public List<IISLogEvent> EntityLogEvents { get; set; } = [];
    public string ConnectionString { get; set; } = default!;

    private bool _isDatabaseTask;
    private readonly string _zipper = $"{AppDomain.CurrentDomain.BaseDirectory}7-Zip\\7z.exe";

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
            ExtractZipFiles(iPath, iOutput);
        else
            Console.WriteLine("no execution command found!");
    }

    private void ExtractZipFiles(int iPath, int iOutput = -1)
    {
        try
        {
            OriginalDirectory = $"{AppDomain.CurrentDomain.BaseDirectory}output";
            string? outputDirectory = (iOutput != -1) ? Parameters[iOutput + 1] : null;

            string extractArgs = $@"x {Parameters[iPath + 1]} -o{OriginalDirectory}";
            Console.WriteLine("Please Wait!\n This Could Take a While! ...");

            if (Directory.GetFileSystemEntries(OriginalDirectory).Length == 0)
                Processor.InvokeProcess(_zipper, extractArgs);
            else
                Console.WriteLine("Directory Exists. Skipping zip extract...");

            Console.WriteLine($"\n Files Extracted: {OriginalDirectory}\n Creating Database and building directory structure...");

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

                    Console.WriteLine($"You may need to manually create the IISLogEvents table in the database..\nEntity Framework Cannot guarantee code first table creation on your database schema programmatically\nAttempting to run Create Script Query -- requires your account to have sufficient privilege through connections string\n\n{sql}");

                    try
                    {
                        context.Database.Database.ExecuteSqlRaw(sql); // FIXME turn this into a sql cmd type
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Execute raw sql failed with message.. {e.Message}\n\n Table may exist... Continuing process...");
                    }

                }
            }

            DirectoryNode root = BuildDirectoryStructure(OriginalDirectory);
            Console.WriteLine($"Deleting Root Directory... Finishing Job {root.Path}");
            Directory.Delete(root.Path);

            if (outputDirectory != null)
                File.Move($"{AppDomain.CurrentDomain.BaseDirectory}localdb", outputDirectory);
        }
        catch (Exception e)
        {
            Console.WriteLine($"zip path file not found error!\nDetails:\n\t{e.Message}");
        }
    }

    private DirectoryNode BuildDirectoryStructure(string directory, DirectoryNode node = null, bool first = true)
    {
        node ??= new DirectoryNode();

        if (Directory.Exists(directory))
        {
            string[] dirs = Directory.GetDirectories(directory);

            if (dirs.Length > 0)
            {
                foreach (string dirPath in dirs)
                {
                    if (first)
                    {
                        node.Name = GetFileName(dirPath);
                        node.Path = dirPath;
                        node.Type = FileType.DIR;

                        first = false;
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
                foreach (string path in Directory.GetFiles(directory))
                {
                    if (first)
                    {
                        node.Name = GetFileName(path);
                        node.Path = path;
                        node.Type = node.Name.Contains('~') ? FileType.TXT : FileType.ZIP;

                        first = false;
                    }
                    else
                    {
                        string name = GetFileName(path);
                        node.Next = new DirectoryNode(name, path)
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

            first = true;

            return Unzip_File_Execute_SQL_Task_And_Recurse_Directory(directory, node, first);
        }

        if (node.Previous != null)
            return BuildDirectoryStructure(node.Previous.Path, node.Previous, first);
        else if (node.Parent != null)
            return BuildDirectoryStructure(node.Parent.Path, node.Parent, first);
        else
            return node;
    }

    /// <summary>
    /// Extract file name from its full path
    /// </summary>
    /// <param name="path">Full path for file</param>
    /// <returns>Returns file name with extension</returns>
    private static string GetFileName(string path) => path.Split("\\").Last();

    private DirectoryNode Unzip_File_Execute_SQL_Task_And_Recurse_Directory(string directory, DirectoryNode node, bool first, Func<DirectoryNode, bool> cleanupNode = null, bool isParent = false)
    {
        Console.Clear();
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
                    Unzip_File_Execute_SQL_Task_And_Recurse_Directory(directory, node, first);
            }
        }

        string? previousDirectory = directory;
        directory = (node.Name != null && !directory.Contains(node.Name)) ? $"{directory}\\{node.Name}" : directory;

        if (Directory.Exists(directory))
        {
            if (Directory.GetFileSystemEntries(directory).Length == 0)
            {
                if (node.Previous != null && node.Path.Equals(directory))
                {
                    node = node.Previous;
                    Cleanup(ref node);
                }
                else if (node.Previous != null && !node.Path.Equals(directory))
                {
                    Directory.Delete(directory);
                    directory = node.Path;
                }
                else if (node.Parent != null)
                {
                    node = node.Parent;
                    Cleanup(ref node, true);
                }
                else if (node.Parent == null)
                {
                    node.Path = directory;

                    return node;
                }
            }

            return BuildDirectoryStructure(directory, node.Inside, first);
        }
        else
        {
            if (node.Type.Equals(FileType.ZIP))
            {
                Console.WriteLine("Unzipping contents of inner zip files...May take a while.. ");
                string extractArgs = $@"x {directory} -o{previousDirectory}";
                Processor.InvokeProcess(_zipper, extractArgs);

                Console.WriteLine("Deleting previous zip file.. ");
                File.Delete(directory);
                node.Name += (node.Name.Contains('~')) ? string.Empty : "~";
                node.Path += (node.Path.Contains('~')) ? string.Empty : "~";
                node.Type = FileType.TXT;
            }

            if (node.Type.Equals(FileType.TXT))
            {
                #region DESERIALIZATION
                string json = File.ReadAllText(node.Path);

                // normalize to JSON structure?
                json = json.Insert(0, "[") + "]";
                json = json.Replace("}{", "},{");

                List<IISLog>? logEventList = JsonSerializer.Deserialize<List<IISLog>>(json);

                logEventList.ForEach(iisLog => EntityLogEvents.AddRange(iisLog.logEvents.Select(s => new IISLogEvent
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
                            context.IISLogEvents.AddRange(EntityLogEvents);
                            context.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Caught Error:\n{ex.Message}\n Retrying by Detatching Entities and saving individually modified...");
                            context.AttachSaveEntities(EntityLogEvents);
                        }
                    }

                    EntityLogEvents = [];

                    Console.WriteLine("Changes Saved to SQLite DB! \nYou can use Query Syntax -SQL to query data\nYou can take the local.db file and upload into SQLite db browser or MS Access");
                }
            }

            node.Inside = null;
            string[] parts = previousDirectory.Split("\\");
            directory = string.Join("\\", parts, 0, parts.Length - 1);

            if (node.Previous != null)
                return Unzip_File_Execute_SQL_Task_And_Recurse_Directory(previousDirectory, node.Previous, first, x => Cleanup(ref x));
            else if (!directory.Equals(OriginalDirectory) && node.Parent != null)
                return Unzip_File_Execute_SQL_Task_And_Recurse_Directory(directory, node.Parent, first, (x) => Cleanup(ref x), true);
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
