using AWSS3Zip.Entity.Contracts;
using AWSS3Zip.Models;
using Microsoft.EntityFrameworkCore;

namespace AWSS3Zip.Entity
{
    public class DatabaseContext : IDatabaseContext<AppDatabase>, IDisposable
    {
        public AppDatabase Database { get; set; }
        public DB Type { get; set; }

        private const string _defaultDb = "Data Source=localdb.db";

        private bool _disposed;

        public AppDatabase Build(string connection)
        {
            if (Database == null)
            {
                DbContextOptionsBuilder<AppDatabase> optionsBuilder = new();

                if (string.IsNullOrEmpty(connection))
                {
                    optionsBuilder.EnableSensitiveDataLogging().UseSqlite(_defaultDb);
                    Type = DB.SQLite;
                }
                else
                {
                    optionsBuilder.EnableSensitiveDataLogging().UseSqlServer(connection);
                    Type = DB.Microsoft;
                }

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
}
