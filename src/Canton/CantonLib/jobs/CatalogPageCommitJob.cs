using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            Dictionary<int, CloudQueueMessage> unQueuedMessages = new Dictionary<int, CloudQueueMessage>();
            Queue<CloudQueueMessage> orderedMessages = new Queue<CloudQueueMessage>();

            var blobClient = Account.CreateCloudBlobClient();

            Stopwatch giveup = new Stopwatch();
            giveup.Start();

            using (AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(Storage, 600))
            {
                var messages = Queue.GetMessages(32, hold).ToList();

                // tasks that will be waited on as part of the commit
                Queue<Task> tasks = new Queue<Task>();

                // everything must run in canton commit order!
                while (messages.Count > 0 || unQueuedMessages.Count > 0 || orderedMessages.Count > 0)
                {
                    Log(String.Format("Messages: {0} Waiting: {1} Ordered: {2}", messages.Count, unQueuedMessages.Count, orderedMessages.Count));

                    foreach (var message in messages)
                    {
                        JObject json = JObject.Parse(message.AsString);
                        int id = json["cantonCommitId"].ToObject<int>();

                        if (id >= cantonCommitId)
                        {
                            unQueuedMessages.Add(id, message);
                        }
                        else
                        {
                            LogError("Ignoring old cantonCommitId: " + id + " We are on: " + cantonCommitId);
                            tasks.Enqueue(Queue.DeleteMessageAsync(message));
                        }
                    }

                    while (unQueuedMessages.ContainsKey(cantonCommitId))
                    {
                        orderedMessages.Enqueue(unQueuedMessages[cantonCommitId]);
                        unQueuedMessages.Remove(cantonCommitId);
                        cantonCommitId++;

                        giveup.Restart();
                    }

                    while (orderedMessages.Count > 0)
                    {
                        var message = orderedMessages.Dequeue();
                        JObject json = JObject.Parse(message.AsString);
                        int curId = json["cantonCommitId"].ToObject<int>();

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
                            obj.Add("cantonCommitId", curId);
                            Log("Cursor cantonCommitId: " + curId);

                            Cursor.Position = DateTime.UtcNow;
                            Cursor.Metadata = obj;
                            tasks.Enqueue(Cursor.Save());

                            Task.WaitAll(tasks.ToArray());
                            tasks.Clear();
                        }
                    }

                    messages = Queue.GetMessages(32, hold).ToList();

                    if (messages.Count < 1)
                    {
                        // avoid getting out of control when the pages aren't ready yet
                        Log("PageCommitJob Waiting for: " + cantonCommitId);
                        Thread.Sleep(TimeSpan.FromSeconds(10));

                        // just give up after 5 minutes 
                        // TODO: handle this better
                        if (giveup.Elapsed > TimeSpan.FromMinutes(5))
                        {
                            while (!unQueuedMessages.ContainsKey(cantonCommitId))
                            {
                                LogError("Giving up on: " + cantonCommitId);
                                cantonCommitId++;
                            }
                        }
                    }
                }

                if (writer.Count > 0)
                {
                    tasks.Enqueue(writer.Commit());

                    // update the cursor
                    JObject obj = new JObject();
                    obj.Add("cantonCommitId", cantonCommitId);
                    Log("cantonCommitId: " + cantonCommitId);

                    Cursor.Position = DateTime.UtcNow;
                    Cursor.Metadata = obj;
                    tasks.Enqueue(Cursor.Save());

                    Task.WaitAll(tasks.ToArray());
                    tasks.Clear();
                }
            }
        }
    }
}
