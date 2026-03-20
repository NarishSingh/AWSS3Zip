using AWSS3Zip.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace AWSS3Zip.Entity.Contracts;

public interface IAppDatabase
{
    public DbSet<IISLogEvent> IISLogEvents { get; set; }

    public string ConnectionString { get; set; }
    public AppDatabase DetachEntities();
    public void Attach_And_Save_Entities(List<IISLogEvent> newEntities);
}
