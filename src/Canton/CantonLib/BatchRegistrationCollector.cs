using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class BatchRegistrationCollector : RegistrationCatalogCollector
    {

        public BatchRegistrationCollector(StorageFactory factory, int batchSize)
            : base(factory, batchSize)
        {

        }

    }
}
