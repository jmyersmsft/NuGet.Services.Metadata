using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class RegistrationJob : CollectorJob
    {
        private readonly StorageFactory _factory;
        private const int BatchSize = 200;
        private DateTime _lastCommit;

        public RegistrationJob(Config config, Storage storage, StorageFactory factory)
            : base(config, storage, "registrations")
        {
            _factory = factory;
        }

        public override async Task RunCore()
        {
            RegistrationCatalogCollector collector = new RegistrationCatalogCollector(_factory, BatchSize);

            var end = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(2));

            CollectorCursor cursor = new CollectorCursor(Cursor.Position);
            CollectorCursor endCursor = new CollectorCursor(end);

            CollectorHttpClient httpClient = new CollectorHttpClient();

            var indexUri = new Uri(Config.GetProperty("CatalogIndex"));
            await collector.Run(httpClient, indexUri, cursor);
            collector.ProcessedCommit += collector_ProcessedCommit;

            Cursor.Position = _lastCommit;
            await Cursor.Save();
        }

        private void collector_ProcessedCommit(CollectorCursor obj)
        {
            _lastCommit = DateTime.Parse(obj.Value);
            Log("Processing Registrations: " + obj.Value);
        }
    }
}
