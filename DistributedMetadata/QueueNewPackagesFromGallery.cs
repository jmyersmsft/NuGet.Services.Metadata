using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.DistributedWork
{
    public class QueueNewPackagesFromGallery : DistributedJob
    {

        public QueueNewPackagesFromGallery(CollectorCursor start, CollectorCursor end)
            : base(start, end)
        {

        }

        public override async Task RunCore()
        {

        }

    }
}
