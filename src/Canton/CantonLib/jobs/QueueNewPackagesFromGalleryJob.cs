using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.GalleryIntegration;
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

        public QueueNewPackagesFromGallery(Config config)
            : base(config, CursorName)
        {

        }

        public override async Task RunCore()
        {
            DateTime start = Cursor.Position;
            DateTime end = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(15));

            var client = Account.CreateCloudQueueClient();
            var queue = client.GetQueueReference(CantonConstants.UploadQueue);
            string dbConnStr = Config.GetProperty("GalleryConnectionString");

            // Load storage
            Storage storage = new AzureStorage(Account, Config.GetProperty("GalleryPageContainer"));
            using (var writer = new AppendOnlyCatalogWriter(storage))
            {
                var batcher = new GalleryExportBatcher(BatchSize, writer);
                int lastHighest = 0;
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
                batcher.Complete().Wait();
            }

            using (SqlConnection connection = new SqlConnection(Config.GetProperty("GalleryConnectionString")))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(_cmdText, connection);
                command.Parameters.AddWithValue("since", start);
                command.Parameters.AddWithValue("end", end);

                SqlDataReader reader = command.ExecuteReader();

                Task queueTask = null;

                while (reader.Read())
                {
                    string version = reader.GetString(0);
                    string id = reader.GetString(1);
                    DateTime created = reader.GetDateTime(2);
                    string nupkgName = string.Format("{0}.{1}.nupkg", id, version).ToLowerInvariant();

                    JObject summary = new JObject();
                    summary.Add("id", id);
                    summary.Add("version", version);
                    summary.Add("created", created.ToString("O"));
                    summary.Add("nupkg", nupkgName);
                    summary.Add("submitted", DateTime.UtcNow.ToString("O"));
                    summary.Add("failures", 0);
                    summary.Add("host", Host);

                    if (queueTask != null)
                    {
                        await queueTask;
                    }

                    queueTask = queue.AddMessageAsync(new CloudQueueMessage(summary.ToString()));

                    Log("Reporting upload: " + nupkgName);
                }

                if (queueTask != null)
                {
                    await queueTask;
                }
            }
        }
    }
}
