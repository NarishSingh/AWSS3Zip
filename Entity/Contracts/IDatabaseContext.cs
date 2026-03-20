using AWSS3Zip.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AWSS3Zip.Entity.Contracts
{
    public interface IDatabaseContext<Y> 
    {
        public Y Database { get; set; }
        public DB Type { get; set; }

        public DatabaseContext AddConnection(string connectionString);
        public Y Build(string connect);

    }
}
