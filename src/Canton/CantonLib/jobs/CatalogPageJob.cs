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

        public CatalogPageJob(Config config, string queueName)
            : base(config, queueName)
        {
            _packagesStorageAccount = CloudStorageAccount.Parse(config.GetProperty("PackagesStorageConnectionString"));
        }

        public override async Task RunCore()
        {
            TimeSpan hold = TimeSpan.FromMinutes(90);

            CloudQueueMessage message = Queue.GetMessage(hold);

            AzureStorage storage = new AzureStorage(Account, Config.GetProperty("CatalogContainer"));
            var qClient = Account.CreateCloudQueueClient();
            var queue = qClient.GetQueueReference(CantonConstants.CatalogPageQueue);

            while (message != null)
            {
                JObject work = JObject.Parse(message.AsString);
                Uri galleryPageUri = new Uri(work["uri"].ToString());

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

                Action<Uri, Uri> handler = (resourceUri, pageUri) => QueuePage(resourceUri, pageUri, queue);

                // create the new catalog item
                using (var stream = nupkg.OpenRead())
                {
                    // Create the catalog page and index section only
                    using (CatalogPageCreator writer = new CatalogPageCreator(storage, handler))
                    {
                        CatalogItem catalogItem = Utils.CreateCatalogItem(stream, published, null, nupkg.FullName);
                        writer.Add(catalogItem);
                        await writer.Commit(DateTime.UtcNow);
                    }
                }

                Queue.DeleteMessage(message);
                message = Queue.GetMessage(hold);
            }
        }

        private void QueuePage(Uri resourceUri, Uri pageUri, CloudQueue queue)
        {
            JObject summary = new JObject();
            summary.Add("resourceUri", resourceUri.AbsoluteUri);
            summary.Add("pageUri", pageUri.AbsoluteUri);
            summary.Add("submitted", DateTime.UtcNow.ToString("O"));
            summary.Add("failures", 0);
            summary.Add("host", Host);

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

        private void CreatePage()
        {

        }
    }
}
