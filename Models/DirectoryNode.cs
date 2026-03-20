namespace AWSS3Zip.Models;

public class DirectoryNode(string name = null, string path = null)
{
    public string Name { get; set; } = name;
    public string Path { get; set; } = path;

    public DirectoryNode Parent { get; set; }
    public DirectoryNode Inside { get; set; }
    public DirectoryNode Previous { get; set; }
    public DirectoryNode Next { get; set; }

    public FileType Type { get; set; }
}
