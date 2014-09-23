using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.WarehouseIntegration
{
    public class StatsItemCollector : BatchCollector
    {
        public StatsItemCollector(int countLimit)
            : base(200)
        {
            CountLimit = countLimit;
            CountLimitCounter = 0;
            Result = new JArray();
        }

        private int CountLimit { get; set; }

        private int CountLimitCounter { get; set; }

        public JArray Result { get; set; }

        protected async override Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            List<JObject> selectedItems = new List<JObject>();
            List<Task<string>> tasks = new List<Task<string>>();

            foreach (JObject item in items)
            {
                if (++CountLimitCounter > CountLimit)
                    break;

                Uri itemUri = item["url"].ToObject<Uri>();
                tasks.Add(client.GetStringAsync(itemUri));
                selectedItems.Add(item);
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
                for (int i = 0; i < tasks.Count; i++)
                {
                    JArray statsCatalogItemData = JArray.Parse(tasks[i].Result);
                    selectedItems[i]["data"] = statsCatalogItemData;
                    Result.Add(selectedItems[i]);
                }
            }
        }
    }
}
