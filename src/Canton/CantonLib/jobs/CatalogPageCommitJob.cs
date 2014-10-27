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

        private Dictionary<int, string> GetWork()
        {
            TimeSpan hold = TimeSpan.FromMinutes(90);

            Dictionary<int, string> all = new Dictionary<int, string>();

            var messages = Queue.GetMessages(32, hold).ToList();

            while (messages.Count > 0)
            {
                foreach (var message in messages)
                {
                    string s = message.AsString;
                    JObject json = JObject.Parse(s);
                    int curId = json["cantonCommitId"].ToObject<int>();

                    if (!all.ContainsKey(curId))
                    {
                        all.Add(curId, s);
                    }
                    else
                    {
                        Console.WriteLine("Dupe id!");
                    }
                }

                Parallel.ForEach(messages, message =>
                    {
                        Queue.DeleteMessage(message);
                    });

                // stop if we aren't maxing out
                if (messages.Count == 32)
                {
                    messages = Queue.GetMessages(32, hold).ToList();
                }
                else
                {
                    messages = new List<CloudQueueMessage>();
                }
            }

            return all;
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

            Queue<JObject> orderedMessages = new Queue<JObject>();

            var blobClient = Account.CreateCloudBlobClient();

            Stopwatch giveup = new Stopwatch();
            giveup.Start();

            Dictionary<int, string> unQueuedMessages = new Dictionary<int, string>();

            try
            {
                using (AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(Storage, 600))
                {
                    var newWork = GetWork();

                    // tasks that will be waited on as part of the commit
                    Queue<Task> tasks = new Queue<Task>();
                    Stack<CantonCatalogItem> itemStack = new Stack<CantonCatalogItem>();

                    // everything must run in canton commit order!
                    while (newWork.Count > 0 || unQueuedMessages.Count > 0 || orderedMessages.Count > 0)
                    {
                        Log(String.Format("New: {0} Waiting: {1} Ordered: {2}", newWork.Count, unQueuedMessages.Count, orderedMessages.Count));

                        int[] newIds = newWork.Keys.ToArray();

                        foreach (int curId in newIds)
                        {
                            string s = newWork[curId];
                            JObject json = JObject.Parse(s);
                            int id = json["cantonCommitId"].ToObject<int>();

                            if (id >= cantonCommitId && !unQueuedMessages.ContainsKey(id))
                            {
                                unQueuedMessages.Add(id, s);
                            }
                            else
                            {
                                LogError("Ignoring old cantonCommitId: " + id + " We are on: " + cantonCommitId);
                            }
                        }

                        while (unQueuedMessages.ContainsKey(cantonCommitId) && unQueuedMessages.Count < 3000)
                        {
                            JObject json = JObject.Parse(unQueuedMessages[cantonCommitId]);

                            orderedMessages.Enqueue(json);
                            unQueuedMessages.Remove(cantonCommitId);
                            cantonCommitId++;

                            giveup.Restart();
                        }

                        while (orderedMessages.Count > 0)
                        {
                            JObject json = orderedMessages.Dequeue();
                            int curId = json["cantonCommitId"].ToObject<int>();
                            string resourceUriString = json["uri"].ToString();

                            if (StringComparer.OrdinalIgnoreCase.Equals(resourceUriString, "https://failed"))
                            {
                                Log("Skipping failed page: " + cantonCommitId);
                                continue;
                            }

                            Uri resourceUri = new Uri(resourceUriString);

                            // the page is loaded from storage in the background
                            CantonCatalogItem item = new CantonCatalogItem(Account, resourceUri);
                            itemStack.Push(item);
                            writer.Add(item);

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

                                try
                                {
                                    // clean up our items
                                    foreach (var cc in itemStack)
                                    {
                                        // TODO: clean up the tmp page also
                                        cc.Dispose();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError("item dispose failure: " + ex.ToString());
                                }
                            }
                        }

                        // get the next work item
                        if (_run)
                        {
                            newWork = GetWork();
                        }
                        else
                        {
                            newWork = new Dictionary<int, string>();
                        }

                        if (newWork.Count < 1 && _run)
                        {
                            // avoid getting out of control when the pages aren't ready yet
                            Log("PageCommitJob Waiting for: " + cantonCommitId);
                            Thread.Sleep(TimeSpan.FromSeconds(10));

                            // just give up after 5 minutes 
                            // TODO: handle this better
                            if (giveup.Elapsed > TimeSpan.FromMinutes(5) || unQueuedMessages.Count > 5000)
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
            finally
            {
                Log("returning work to the queue");

                // put everything back into the queue
                ParallelOptions options = new ParallelOptions();
                options.MaxDegreeOfParallelism = 128;

                Parallel.ForEach(orderedMessages, options, json =>
                    {
                        Queue.AddMessage(new CloudQueueMessage(json.ToString()));
                    });

                Parallel.ForEach(unQueuedMessages.Values, options, s =>
                {
                    Queue.AddMessage(new CloudQueueMessage(s));
                });

                Log("returning work to the queue done");
            }
        }
    }
}
