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
    public string[] Parameters { get; set; }
    public string OriginalDirectory { get; set; }
    public List<IISLogEvent> EntityLogEvents { get; set; }
    public string ConnectionString { get; set; } = default!;

    bool _isDatabaseTask;

    public ExtractJob BuildParameters(string[] parameters)
    {
        Parameters = parameters;
        EntityLogEvents = [];
        return this;
    }

    public void Execute()
    {
        int iPath = Array.IndexOf(Parameters, "-e");
        int dbPosition = Array.IndexOf(Parameters, "-db") + Array.IndexOf(Parameters, "--database") + 1;

        _isDatabaseTask = dbPosition >= 0;

        if (_isDatabaseTask && Parameters.Length > dbPosition + 1 && Parameters[dbPosition + 1].Contains("Server="))
            ConnectionString = Parameters[dbPosition + 1];

        if (iPath == -1) Array.IndexOf(Parameters, "--extract");

        if (iPath != -1 && (Parameters[iPath + 1].Contains('-') || Parameters[iPath + 1].Contains("--")))
        {
            Console.WriteLine("Command not formatted Correctly, contains '-' or '--' followed by command variable");
            return;
        }

        int iOutput = Array.IndexOf(Parameters, "-o");
        if (iOutput == -1) Array.IndexOf(Parameters, "--output");
        if (iOutput != -1 && (Parameters[iOutput + 1].Contains('-') || Parameters[iOutput + 1].Contains("--")))
        {
            Console.WriteLine("Command not formatted Correctly, contains '-' or '--' followed by command variable");
            return;
        }

        if (iPath != -1)
            ExtractZipFiles(iPath, iOutput);

        else Console.WriteLine("no execution command found!");
    }

    private void ExtractZipFiles(int iPath, int iOutput = -1)
    {
        try
        {
            string outputDirectory = (iOutput != -1) ? Parameters[iOutput + 1] : null;
            OriginalDirectory = $"{AppDomain.CurrentDomain.BaseDirectory}output";
            string? command = $"{AppDomain.CurrentDomain.BaseDirectory}7-Zip\\7z.exe";
            string? arguments = $@"x {Parameters[iPath + 1]} -o{OriginalDirectory}";
            Console.WriteLine("Please Wait!\n This Could Take a While! ....");

            if (Directory.GetFileSystemEntries($"{AppDomain.CurrentDomain.BaseDirectory}output").Length == 0)
                Processor.InvokeProcess(command, arguments);
            else
                Console.WriteLine("Directory Exists. Skipping zip extract...");

            Console.WriteLine($"\n Files Extracted: {OriginalDirectory}\n Creating Database and building directory structure...");

            if (_isDatabaseTask)
            {
                using DatabaseContext? context = new();

                context.Build(ConnectionString).Database.EnsureCreated();
                context.Database.SaveChanges();
                if (context.Type == SQLType.Microsoft)
                {
                    string? textSQL = File.ReadAllText($"{AppDomain.CurrentDomain.BaseDirectory}Text\\CreateTable.txt");
                    Console.WriteLine($"You may need to manually create the IISLogEvents table in the database..\nEntity Framework Cannot guarantee code first table creation on your database schema programmatically\nAttempting to run Create Script Query -- requires your account to have sufficient privilege through connections string\n\n{textSQL}");

                    try
                    {
                        context.Database.Database.ExecuteSqlRaw(textSQL);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Execute raw sql failed with message.. {e.Message}\n\n Table may exist.. Continuing process..");
                    }

                }
            }

            DirectoryNode root = BuildDirectoryStructure(OriginalDirectory);
            Console.WriteLine($"Deleting Root Directory.. Finishing Job {root.Path}");
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
            string[] directoryFolders = Directory.GetDirectories(directory);

            if (directoryFolders.Length > 0)
            {
                foreach (string folder in directoryFolders)
                {
                    if (first)
                    {
                        node.Name = folder.Split("\\").Last().ToString();
                        node.Path = $"{folder}";
                        node.Type = FileType.Folder;

                        first = false;
                        node.Inside = new DirectoryNode() { Parent = node };

                    }
                    else
                    {
                        node.Next = new DirectoryNode(folder.Split("\\").Last(), $"{folder}")
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
                foreach (string file in Directory.GetFiles(directory))
                {
                    if (first)
                    {
                        node.Name = file.Split("\\").Last().ToString(); ;
                        node.Path = $"{file}";
                        node.Type = node.Name.Contains('~') ? FileType.Text : FileType.Zip;

                        first = false;
                    }
                    else
                    {
                        string? name = file.Split("\\").Last().ToString();
                        node.Next = new DirectoryNode(name, $"{file}")
                        {
                            Previous = node,
                            Parent = node.Parent,
                            Type = name.Contains('~') ? FileType.Text : FileType.Zip
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
        {
            return BuildDirectoryStructure(node.Previous.Path, node.Previous, first);
        }
        else if (node.Parent != null)
        {
            return BuildDirectoryStructure(node.Parent.Path, node.Parent, first);
        }
        else
        {
            return node;
        }
    }

    private DirectoryNode Unzip_File_Execute_SQL_Task_And_Recurse_Directory(string directory, DirectoryNode node, bool first, Func<DirectoryNode, bool> cleanupNode = null, bool isParent = false)
    {
        Console.Clear();
        if (cleanupNode != null)
        {
            if (isParent && node.Inside != null)
            {
                if (node.Inside.Type == FileType.Folder)
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
        directory = (node.Name != null && !directory.Contains(node.Name)) ? $"{directory}\\{node.Name}" : $"{directory}";

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
            if (node.Type.Equals(FileType.Zip))
            {
                Console.WriteLine("Unzipping contents of inner zip files...May take a while.. ");
                string? command = $"{AppDomain.CurrentDomain.BaseDirectory}7-Zip\\7z.exe";
                string? arguments = $@"x {directory} -o{previousDirectory}";
                Processor.InvokeProcess(command, arguments);

                Console.WriteLine("Deleting previous zip file.. ");
                File.Delete(directory);
                node.Name += (node.Name.Contains('~')) ? "" : "~";
                node.Path += (node.Path.Contains('~')) ? "" : "~";
                node.Type = FileType.Text;
            }

            if (node.Type.Equals(FileType.Text))
            {
                var json = File.ReadAllText(node.Path);

                json = json.Insert(0, "[") + "]";
                json = json.Replace("}{", "},{");

                List<IISLog>? logEventList = JsonSerializer.Deserialize<List<IISLog>>(json);

                logEventList.ForEach(x =>
                {
                    EntityLogEvents.AddRange(
                        x.logEvents.Select(s => new IISLogEvent()
                        {
                            Id = s.id,
                            MessageType = x.messageType,
                            Owner = x.owner,
                            LogGroup = x.logGroup,
                            LogStream = x.logStream,
                            SubscriptionFilters = JsonSerializer.Serialize(x.subscriptionFilters),
                            DateTime = DateTimeOffset.FromUnixTimeMilliseconds(s.timestamp).DateTime,
                            RequestMessage = s.message
                        }));
                });

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
                            Console.WriteLine($"Caught Error:\n{ex.Message}\n Retrying by Detatching Entities and saving individually modified..");
                            context.Attach_And_Save_Entities(EntityLogEvents);
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
            {
                return Unzip_File_Execute_SQL_Task_And_Recurse_Directory(previousDirectory, node.Previous, first, x => Cleanup(ref x));
            }
            else if (!directory.Equals(OriginalDirectory) && node.Parent != null)
            {
                return Unzip_File_Execute_SQL_Task_And_Recurse_Directory(directory, node.Parent, first, (x) => Cleanup(ref x), true);
            }
            else
            {
                return node;
            }
        }
    }

    private bool Cleanup(ref DirectoryNode node, bool isParent = false)
    {
        if (!isParent)
        {
            if (node.Next.Type == FileType.Folder)
                Directory.Delete(node.Next.Path);
            else if (node.Next.Type == FileType.Text)
                File.Delete(node.Next.Path);

            node.Next = null;
        }
        else
        {
            if (node.Inside.Type == FileType.Folder && node.Inside.Path != null)
                Directory.Delete(node.Inside.Path);
            else if (node.Inside.Type == FileType.Text)
                File.Delete(node.Inside.Path);

            node.Inside = null;
        }
        return true;
    }
}
