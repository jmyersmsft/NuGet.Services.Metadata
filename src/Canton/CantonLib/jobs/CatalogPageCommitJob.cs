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

        public CatalogPageCommitJob(Config config)
            : base(config, CantonConstants.CatalogPageQueue)
        {

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
                Uri resourceUri = new Uri(work["resourceUri"].ToString());
                Uri pageUri = new Uri(work["pageUri"].ToString());

                using (AppendOnlyCatalogWriter writer = new AppendOnlyCatalogWriter(storage, 600))
                {

                }

                // get the next work item
                Queue.DeleteMessage(message);
                message = Queue.GetMessage(hold);
            }
        }
    }
}
