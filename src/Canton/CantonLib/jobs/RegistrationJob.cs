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
            DateTime now = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(30));
            _lastCommit = Cursor.Position;

            RegistrationCatalogCollector collector = new RegistrationCatalogCollector(_factory, BatchSize);
            collector.ContentBaseAddress = new Uri(Config.GetProperty("ContentBaseAddress"));

            var end = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(2));

            CollectorCursor cursor = new CollectorCursor(Cursor.Position);
            CollectorCursor endCursor = new CollectorCursor(end);

            CollectorHttpClient httpClient = new CollectorHttpClient();

            collector.ProcessedCommit += Collector_ProcessedCommit;

            var indexUri = new Uri(Config.GetProperty("CatalogIndex"));
            await collector.Run(httpClient, indexUri, cursor);

            collector.ProcessedCommit -= Collector_ProcessedCommit;

            Cursor.Position = _lastCommit;

            if (Cursor.Position.CompareTo(now) < 0)
            {
                Cursor.Position = now;
            }

            Log("Requests: " + collector.RequestCount);
            Log("Saving cursor: " + Cursor.Position.ToString("O"));

            await Cursor.Save();
        }

        private void Collector_ProcessedCommit(CollectorCursor obj)
        {
            _lastCommit = DateTime.Parse(obj.Value);
            Log("Processing Registrations: " + obj.Value);
        }
    }
}
