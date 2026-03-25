namespace AWSS3Zip.Models;

public class DirectoryNode(string name = default!, string path = default!)
{
    public string Name { get; set; } = name;
    public string Path { get; set; } = path;

    public DirectoryNode Parent { get; set; } = default!;
    public DirectoryNode Inside { get; set; } = default!;
    public DirectoryNode Previous { get; set; } = default!;
    public DirectoryNode Next { get; set; } = default!;

    public FileType Type { get; set; }
}
