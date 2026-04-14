using AWSS3Zip.Entity.Contracts;
using AWSS3Zip.Models;
using Microsoft.EntityFrameworkCore;

namespace AWSS3Zip.Entity;

public class DatabaseContext : IDatabaseContext<AppDatabase>, IDisposable
{
    private const string _local = "Data Source=localdb.db";
    private bool _disposed;

    public AppDatabase Database { get; set; }
    public DB Type { get; set; }

    public AppDatabase Build(string connection)
    {
        if (Database == null)
        {
            DbContextOptionsBuilder<AppDatabase> optionsBuilder = new();

            // FIXME OUTPUTTING BOTH
            /*
            if (string.IsNullOrEmpty(connection))
            {
                optionsBuilder.EnableSensitiveDataLogging().UseSqlite(_local);
                Type = DB.SQLite;
            }
            else
            {
                optionsBuilder.EnableSensitiveDataLogging().UseSqlServer(connection);
                Type = DB.Microsoft;
            }
            */

            // persist to sqlite and server
            optionsBuilder.EnableSensitiveDataLogging().UseSqlite(_local);
            //optionsBuilder.EnableSensitiveDataLogging().UseSqlServer(connection);

            Database = new AppDatabase(optionsBuilder.Options);
        }

        return Database;
    }

    public AppDatabase Detach() => Database.DetachEntities();

    #region DISPOSAL PATTERN
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
            Database?.Dispose();

        _disposed = true;
    }
    #endregion
}
