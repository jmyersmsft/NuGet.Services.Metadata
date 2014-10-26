using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class CatalogPageCommitJob : CursorQueueFedJob
    {
        private const int BatchSize = 64;

        public CatalogPageCommitJob(Config config, Storage storage)
            : base(config, storage, CantonConstants.CatalogPageQueue, "catalogpagecommit")
        {

        }

        public override async Task RunCore()
        {
            TimeSpan hold = TimeSpan.FromMinutes(90);
            int cantonCommitId = 0;

            JToken cantonCommitIdToken = null;
            if (Cursor.Metadata.TryGetValue("cantonCommitId", out cantonCommitIdToken))
            {
                cantonCommitId = cantonCommitIdToken.ToObject<int>();
            }

            var qClient = Account.CreateCloudQueueClient();
            var queue = qClient.GetQueueReference(CantonConstants.CatalogPageQueue);

            List<Tuple<int, CloudQueueMessage>> extraMessages = new List<Tuple<int, CloudQueueMessage>>();
            Queue<Tuple<int, CloudQueueMessage>> orderedMessages = new Queue<Tuple<int, CloudQueueMessage>>();

            var blobClient = Account.CreateCloudBlobClient();

            using (AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(Storage, 600))
            {
                var messages = queue.GetMessages(32, hold).ToList();

                // tasks that will be waited on as part of the commit
                Queue<Task> tasks = new Queue<Task>();

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
                        cantonCommitId = curTuple.Item1;
                        var message = curTuple.Item2;

                        JObject work = JObject.Parse(message.AsString);
                        Uri resourceUri = new Uri(work["uri"].ToString());

                        // the page is loaded from storage in the background
                        CantonCatalogItem item = new CantonCatalogItem(Account, resourceUri);
                        writer.Add(item);

                        // remove tmp page
                        tasks.Enqueue(item.DeleteBlob());

                        // get the next work item
                        tasks.Enqueue(Queue.DeleteMessageAsync(message));

                        if (writer.Count >= BatchSize)
                        {
                            tasks.Enqueue(writer.Commit());

                            // update the cursor
                            JObject obj = new JObject();
                            obj.Add("cantonCommitId", cantonCommitId);
                            Log("cantonCommitId: " + cantonCommitId);
                            tasks.Enqueue(Cursor.Update(DateTime.UtcNow, obj));

                            Task.WaitAll(tasks.ToArray());
                            tasks.Clear();
                        }
                    }

                    messages = queue.GetMessages(32, hold).ToList();

                    if (messages.Count < 1)
                    {
                        // avoid getting out of control when the pages aren't ready yet
                        Thread.Sleep(TimeSpan.FromMinutes(1));
                    }
                }

                if (writer.Count > 0)
                {
                    tasks.Enqueue(writer.Commit());

                    // update the cursor
                    JObject obj = new JObject();
                    obj.Add("cantonCommitId", cantonCommitId);
                    Log("cantonCommitId: " + cantonCommitId);
                    tasks.Enqueue(Cursor.Update(DateTime.UtcNow, obj));

                    Task.WaitAll(tasks.ToArray());
                    tasks.Clear();
                }
            }
        }
    }
}
