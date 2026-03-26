using AWSS3Zip.Models;

namespace AWSS3Zip.Entity.Contracts;

public interface IDatabaseContext<T>
{
    public T Database { get; set; }
    public DB Type { get; set; }

    public T Build(string connect);
}
