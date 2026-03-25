using AWSS3Zip.Entity.Contracts;
using AWSS3Zip.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AWSS3Zip.Entity
{
    public class DatabaseContext : IDatabaseContext<AppDatabase>, IDisposable
    {
        public AppDatabase Database { get; set; }
        public DB Type { get; set; }

        public string ConnectionString { get; set; }

        private const string DefaultConnection = "Data Source=localdb.db";

        private bool _disposed;

        public DatabaseContext AddConnection(string connectionString)
        {
            ConnectionString = connectionString;
            return this;
        }
        public AppDatabase Build(string connect) => BuildDatabase(connect);

        public AppDatabase Detach() => Database.DetachEntities();


        private AppDatabase BuildDatabase(string connection = null)
        {
            if (Database == null)
            {
                DbContextOptionsBuilder<AppDatabase> optionsBuilder = new();

                if (!(connection ?? ConnectionString).IsNullOrEmpty())
                {
                    optionsBuilder.EnableSensitiveDataLogging().UseSqlServer(connection ?? ConnectionString);
                    Type = DB.Microsoft;
                }
                else
                {
                    optionsBuilder.EnableSensitiveDataLogging().UseSqlite(DefaultConnection);
                    Type = DB.SQLite;
                }

                Database = new AppDatabase(optionsBuilder.Options);
            }

            return Database;
        }

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
    }
}
