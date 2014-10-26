using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class CatalogPageCommitJob : QueueFedJob
    {
        public CatalogPageCommitJob(Config config, Storage storage)
            : base(config, storage, CantonConstants.CatalogPageQueue)
        {

        }

        public override async Task RunCore()
        {
            TimeSpan hold = TimeSpan.FromMinutes(90);
            int cantonCommitId = 0;

            AzureStorage storage = new AzureStorage(Account, Config.GetProperty("CatalogContainer"));
            var qClient = Account.CreateCloudQueueClient();
            var queue = qClient.GetQueueReference(CantonConstants.CatalogPageQueue);

            List<Tuple<int, CloudQueueMessage>> extraMessages = new List<Tuple<int, CloudQueueMessage>>();
            Queue<Tuple<int, CloudQueueMessage>> orderedMessages = new Queue<Tuple<int, CloudQueueMessage>>();

            var blobClient = Account.CreateCloudBlobClient();

            using (AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 600))
            {
                var messages = queue.GetMessages(32, hold).ToList();

                // everything must run in canton commit order!
                while (messages.Count > 0 || extraMessages.Count > 0 || orderedMessages.Count > 0)
                {
                    foreach (var message in messages)
                    {
                        JObject json = JObject.Parse(message.AsString);
                        extraMessages.Add(new Tuple<int, CloudQueueMessage>(json["cantonCommitId"].ToObject<int>(), message));
                    }

                    while (extraMessages.Select(t => t.Item1).Any(x => x == cantonCommitId))
                    {
                        var cur = extraMessages.Where(t => t.Item1 == cantonCommitId).Single();

                        extraMessages.Remove(cur);
                        orderedMessages.Enqueue(cur);
                    }

                    while (orderedMessages.Count > 0)
                    {
                        var curTuple = orderedMessages.Dequeue();
                        var message = curTuple.Item2;

                        JObject work = JObject.Parse(message.AsString);
                        Uri resourceUri = new Uri(work["uri"].ToString());

                        ICloudBlob blob = blobClient.GetBlobReferenceFromServer(resourceUri);

                        CantonCatalogItem item = new CantonCatalogItem(blob);
                        writer.Add(item);

                        // get the next work item
                        Queue.DeleteMessage(message);

                        if (writer.Count >= 600)
                        {
                            await writer.Commit();
                        }
                    }

                    messages = queue.GetMessages(32, hold).ToList();
                }

                if (writer.Count > 0)
                {
                    await writer.Commit();
                }
            }
        }
    }
}
