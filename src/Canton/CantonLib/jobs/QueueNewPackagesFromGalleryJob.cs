using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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

        public QueueNewPackagesFromGallery(Config config)
            : base(config, CursorName)
        {

        }

        // TODO: Remove Top 100
        private const string _cmdText = @"
                    SELECT TOP(100) Packages.[NormalizedVersion], PackageRegistrations.[Id], Packages.Created
                    FROM Packages
                    INNER JOIN PackageRegistrations ON PackageRegistrations.[Key] = Packages.PackageRegistrationKey
                    WHERE Packages.Created > @since AND Packages.Created <= @end
                    ORDER BY Packages.Created
                ";

        public override async Task RunCore()
        {
            DateTime start = Cursor.Position;
            DateTime end = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(15));

            var client = Account.CreateCloudQueueClient();
            var queue = client.GetQueueReference(CantonConstants.UploadQueue);

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
