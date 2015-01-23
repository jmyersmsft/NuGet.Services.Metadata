using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class AzureStorage : Storage
    {
        private CloudBlobDirectory _directory;

        public AzureStorage(CloudStorageAccount account, string containerName)
            : this(account, containerName, String.Empty) { }

        public AzureStorage(CloudStorageAccount account, string containerName, string path, Uri baseAddress)
            : this(account.CreateCloudBlobClient().GetContainerReference(containerName).GetDirectoryReference(path), baseAddress)
        {
        }

        public AzureStorage(CloudStorageAccount account, string containerName, string path)
            : this(account.CreateCloudBlobClient().GetContainerReference(containerName).GetDirectoryReference(path))
        {
        }

        public AzureStorage(CloudBlobDirectory directory)
            : this(directory, GetDirectoryUri(directory))
        {
        }

        public AzureStorage(CloudBlobDirectory directory, Uri baseAddress)
            : base(baseAddress ?? GetDirectoryUri(directory))
        {
            _directory = directory;

            if (_directory.Container.CreateIfNotExists())
            {
                _directory.Container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                if (Verbose)
                {
                    Trace.WriteLine(String.Format("Created '{0}' publish container", _directory.Container.Name));
                }
            }

            ResetStatistics();
        }

        static Uri GetDirectoryUri(CloudBlobDirectory directory)
        {
            Uri uri = new UriBuilder(directory.Uri)
            {
                Scheme = "http",
                Port = 80
            }.Uri;

            return uri;
        }

        //Blob exists
        public override bool Exists(string fileName)
        {
            Uri packageRegistrationUri = ResolveUri(fileName);
            string blobName = GetName(packageRegistrationUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(blobName);

            if (blob.Exists())
            {
                return true;
            }
            return false;
        }

        //  save

        protected override async Task OnSave(Uri resourceUri, StorageContent content)
        {
            string name = GetName(resourceUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);
            blob.Properties.ContentType = content.ContentType;
            blob.Properties.CacheControl = content.CacheControl;

            using (Stream stream = content.GetContentStream())
            {
                await blob.UploadFromStreamAsync(stream);
            }
        }

        //  load

        protected override async Task<StorageContent> OnLoad(Uri resourceUri)
        {
            string name = GetName(resourceUri);

            CloudBlockBlob blob = _directory.GetBlockBlobReference(name);

            if (blob.Exists())
            {
                string content = await blob.DownloadTextAsync();
                return new StringStorageContent(content);
            }

            return null;
        }

        //  delete all

        private async Task DeleteAllBlobsInADirectory(CloudBlobDirectory directoryToDelete)
        {
            var items = directoryToDelete.ListBlobs();
            foreach (IListBlobItem item in items)
            {
                if (item.GetType() == typeof(CloudBlockBlob) || item.GetType().BaseType == typeof(CloudBlockBlob))
                {
                    try
                    {
                        await ((CloudBlockBlob)item).DeleteIfExistsAsync();
                    }
                    catch (Microsoft.WindowsAzure.Storage.StorageException e)
                    {
                        Trace.WriteLine("Blob {0} could not be found" + item.Uri);

                    }
                }
                else if (item.GetType() == typeof(CloudBlobDirectory) || item.GetType().BaseType == typeof(CloudBlobDirectory))
                {
                    await DeleteAllBlobsInADirectory((CloudBlobDirectory)item);
                }
            }
        }

        //delete

        protected override async Task OnDelete(Uri resourceUri)
        {
            string name = GetName(resourceUri);
            string indexBlobName = "index.json";

            if (!String.IsNullOrEmpty(name))
            {
                try
                {
                    CloudBlockBlob blob = _directory.GetBlockBlobReference(name);

                    await blob.DeleteAsync();

                    //Determine if that was the only version for the package
                    //If the delete succeeded and the item count is 1 in index.json, then the only version for this package got deleted
                    Uri indexUri = this.ResolveUri(indexBlobName);
                    string json = await this.LoadString(indexUri);
                    int count = Utils.CountItems(json);

                    //If count is one, clean up index.json
                    if (count == 1)
                    {
                        CloudBlockBlob indexBlob = _directory.GetBlockBlobReference(indexBlobName);
                        await indexBlob.DeleteAsync();
                    }

                }
                catch (Microsoft.WindowsAzure.Storage.StorageException e)
                {
                    Trace.WriteLine("Blob {0} could not be found" + name);

                }
            }
            else //Delete All Versions of a package case
            {
                await DeleteAllBlobsInADirectory(_directory);

            }
        }
    }
}
