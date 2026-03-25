using AWSS3Zip.Entity.Contracts;
using AWSS3Zip.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AWSS3Zip.Entity
{
    public class AppDatabase(DbContextOptions<AppDatabase> options) : DbContext(options), IAppDatabase
    {
        public DbSet<IISLogEvent> IISLogEvents { get; set; }

        public string ConnectionString { get; set; }

        public AppDatabase DetachEntities()
        {
            IEnumerable<EntityEntry> trackedEntities = ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Unchanged);

            foreach (EntityEntry? entry in trackedEntities)
            {
                entry.State = EntityState.Detached;
            }

            ChangeTracker.Clear();

            return this;
        }

        public void AttachSaveEntities(List<IISLogEvent> newEntities)
        {
            int rowCount = int.Parse(IISLogEvents.Max(e => e.Id));
            foreach (IISLogEvent entity in newEntities)
            {
                EntityEntry<IISLogEvent>? existingEntity = ChangeTracker.Entries<IISLogEvent>()
                    .FirstOrDefault(e => e.Entity.Id == entity.Id);

                if (existingEntity != null)
                {
                    entity.RowId = ++rowCount;
                    Entry(existingEntity.Entity).CurrentValues.SetValues(entity);
                }
                else
                {
                    IISLogEvents.Attach(entity);
                }

                try
                {
                    SaveChanges();
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }
    }
}
