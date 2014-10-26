using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class CatalogPageJob : QueueFedJob
    {
        private CloudStorageAccount _packagesStorageAccount;

        public CatalogPageJob(Config config, Storage storage, string queueName)
            : base(config, storage, queueName)
        {
            _packagesStorageAccount = CloudStorageAccount.Parse(config.GetProperty("PackagesStorageConnectionString"));
        }

        public override async Task RunCore()
        {
            TimeSpan hold = TimeSpan.FromMinutes(90);

            CloudQueueMessage message = Queue.GetMessage(hold);

            var qClient = Account.CreateCloudQueueClient();
            var queue = qClient.GetQueueReference(CantonConstants.CatalogPageQueue);

            while (message != null)
            {
                JObject work = JObject.Parse(message.AsString);
                Uri galleryPageUri = new Uri(work["uri"].ToString());
                int cantonCommitId = work["cantonCommitId"].ToObject<int>();
                Log("started cantonCommitId: " + cantonCommitId);

                GraphAddon[] addons = new GraphAddon[] { new OriginGraphAddon(galleryPageUri.AbsoluteUri, cantonCommitId) };

                // read the gallery page
                JObject galleryPage = await GetJson(galleryPageUri);

                string id = galleryPage["id"].ToString();
                string version = galleryPage["version"].ToString();

                DateTime? published = null;
                JToken publishedToken = null;
                if (galleryPage.TryGetValue("published", out publishedToken))
                {
                    published = DateTime.Parse(publishedToken.ToString());
                }

                // download the nupkg
                FileInfo nupkg = await GetNupkg(id, version);

                Action<Uri> handler = (resourceUri) => QueuePage(resourceUri, Schema.DataTypes.PackageDetails, cantonCommitId, queue);

                // create the new catalog item
                using (var stream = nupkg.OpenRead())
                {
                    // Create the core catalog page graph and upload it
                    using (CatalogPageCreator writer = new CatalogPageCreator(Storage, handler, addons))
                    {
                        CatalogItem catalogItem = Utils.CreateCatalogItem(stream, published, null, nupkg.FullName);
                        writer.Add(catalogItem);
                        await writer.Commit(DateTime.UtcNow);
                    }
                }

                // clean up
                nupkg.Delete();

                // get the next work item
                Queue.DeleteMessage(message);
                message = Queue.GetMessage(hold);
            }
        }

        private void QueuePage(Uri resourceUri, Uri itemType, int cantonCommitId, CloudQueue queue)
        {
            JObject summary = new JObject();
            summary.Add("itemType", itemType.AbsoluteUri);
            summary.Add("submitted", DateTime.UtcNow.ToString("O"));
            summary.Add("failures", 0);
            summary.Add("host", Host);
            summary.Add("cantonCommitId", cantonCommitId);
            Log("finished cantonCommitId: " + cantonCommitId);

            queue.AddMessage(new CloudQueueMessage(summary.ToString()));

            Log("Gallery page created: " + resourceUri.AbsoluteUri);
        }

        private async Task<JObject> GetJson(Uri uri)
        {
            var blobClient = Account.CreateCloudBlobClient();
            var galleryBlob = blobClient.GetBlobReferenceFromServer(uri);

            JObject json = null;

            using (MemoryStream stream = new MemoryStream())
            {
                await galleryBlob.DownloadToStreamAsync(stream);

                using (StreamReader reader = new StreamReader(stream))
                {
                    json = JObject.Parse(reader.ReadToEnd());
                }
            }

            return json;
        }

        private async Task<FileInfo> GetNupkg(string id, string version)
        {
            // TODO: Use the MD5 hash on the prod packgae to see if we have the current one

            //NuGetVersion nugetVersion = new NuGetVersion(version);

            var tmpBlobClient = Account.CreateCloudBlobClient();

            string packageName = String.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", id, version).ToLowerInvariant();

            FileInfo file = new FileInfo(Path.Combine(Config.GetProperty("localtmp"), packageName));

            var tmpContainer = tmpBlobClient.GetContainerReference(Config.GetProperty("tmp"));

            string tmpFile = String.Format(CultureInfo.InvariantCulture, "packages/{0}", packageName);
            var tmpBlob = tmpContainer.GetBlockBlobReference(tmpFile);

            if (await tmpBlob.ExistsAsync())
            {
                Log("Downloading from tmp: " + packageName);
                await tmpBlob.DownloadToFileAsync(file.FullName, FileMode.CreateNew);
            }
            else
            {
                var blobClient = _packagesStorageAccount.CreateCloudBlobClient();
                var prodPackages = blobClient.GetContainerReference("packages");
                var prodBlob = prodPackages.GetBlockBlobReference(packageName);

                if (prodBlob.Exists())
                {
                    Log("Downloading from prod: " + packageName);

                    await prodBlob.DownloadToFileAsync(file.FullName, FileMode.CreateNew);

                    // store this in tmp also
                    await tmpBlob.UploadFromFileAsync(file.FullName, FileMode.CreateNew);
                }
                else
                {
                    throw new FileNotFoundException(prodBlob.Uri.AbsoluteUri);
                }
            }

            return file;
        }
    }
}
