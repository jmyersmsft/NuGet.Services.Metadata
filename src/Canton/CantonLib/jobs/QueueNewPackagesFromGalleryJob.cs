using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.GalleryIntegration;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    /// <summary>
    /// Reads the gallery DB and queues new packages.
    /// </summary>
    public class QueueNewPackagesFromGallery : CollectorJob
    {
        public const string CursorName = "queuenewpackagesfromgallery";
        private const int BatchSize = 2000;
        private int _cantonCommitId = 0;

        public QueueNewPackagesFromGallery(Config config, Storage storage)
            : base(config, storage, CursorName)
        {

        }

        public override async Task RunCore()
        {
            int lastHighest = 0;

            JToken lastHighestToken = null;
            if (Cursor.Metadata.TryGetValue("lastHighest", out lastHighestToken))
            {
                lastHighest = lastHighestToken.ToObject<int>();
            }

            JToken cantonCommitIdToken = null;
            if (Cursor.Metadata.TryGetValue("cantonCommitId", out cantonCommitIdToken))
            {
                _cantonCommitId = cantonCommitIdToken.ToObject<int>();
            }

            DateTime end = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(15));

            var client = Account.CreateCloudQueueClient();
            var queue = client.GetQueueReference(CantonConstants.GalleryPagesQueue);
            string dbConnStr = Config.GetProperty("GalleryConnectionString");

            Action<Uri> handler = (resourceUri) => QueuePage(resourceUri, queue);

            Task cursorUpdate = null;

            // Load storage
            using (var writer = new GalleryPageCreator(Storage, handler))
            {
                var batcher = new GalleryExportBatcher(BatchSize, writer);
                while (true)
                {
                    var range = GalleryExport.GetNextRange(
                        dbConnStr,
                        lastHighest,
                        BatchSize).Result;

                    if (range.Item1 == 0 && range.Item2 == 0)
                    {
                        break;
                    }

                    Log(String.Format(CultureInfo.InvariantCulture, "Writing packages with Keys {0}-{1} to catalog...", range.Item1, range.Item2));
                    GalleryExport.WriteRange(
                        dbConnStr,
                        range,
                        batcher).Wait();
                    lastHighest = range.Item2;
                }

                if (cursorUpdate != null)
                {
                    await cursorUpdate;
                }

                // wait for the batch to write
                batcher.Complete().Wait();

                // update the cursor
                JObject obj = new JObject();
                obj.Add("lastHighest", lastHighest);

                // keep track of the order we added these in so that the catalog writer can put them back into order
                obj.Add("cantonCommitId", _cantonCommitId);

                Cursor.Position = DateTime.UtcNow;
                Cursor.Metadata = obj;
                cursorUpdate = Cursor.Save();
            }

            if (cursorUpdate != null)
            {
                await cursorUpdate;
            }
        }

        // Add the page that was created to the queue for processing later
        private void QueuePage(Uri uri, CloudQueue queue)
        {
            JObject summary = new JObject();
            summary.Add("uri", uri.AbsoluteUri);
            summary.Add("submitted", DateTime.UtcNow.ToString("O"));
            summary.Add("failures", 0);
            summary.Add("host", Host);

            summary.Add("cantonCommitId", _cantonCommitId);
            _cantonCommitId++;

            queue.AddMessage(new CloudQueueMessage(summary.ToString()));

            Log("Gallery page created: " + uri.AbsoluteUri);
        }
    }
}
