using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Maintenance;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCatalogCollector : BatchCollector
    {
        StorageFactory _storageFactory;

        public RegistrationCatalogCollector(StorageFactory storageFactory, int batchSize)
            : base(batchSize)
        {
            _storageFactory = storageFactory;
            ItemCount = 0;
        }

        public int ItemCount { get; private set; }

        protected override Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {
            IDictionary<string, IList<JObject>> sortedItems = new Dictionary<string, IList<JObject>>();

            foreach (JObject item in items)
            {
                string key = item["nuget:id"].ToString();

                IList<JObject> itemList;
                if (!sortedItems.TryGetValue(key, out itemList))
                {
                    itemList = new List<JObject>();
                    sortedItems.Add(key, itemList);
                }

                itemList.Add(item);
            }

            //IList<Task> tasks = new List<Task>();

            //foreach (KeyValuePair<string, IList<JObject>> sortedBatch in sortedItems)
            //{
            //    tasks.Add(ProcessSortedBatch(sortedBatch));
            //}

            //return Task.WhenAll(tasks.ToArray());

            foreach (KeyValuePair<string, IList<JObject>> sortedBatch in sortedItems)
            {
                ProcessSortedBatch(sortedBatch).Wait();
            }
            return Task.FromResult(0);
        }

        async Task ProcessSortedBatch(KeyValuePair<string, IList<JObject>> sortedBatch)
        {
            Storage storage = _storageFactory.Create(sortedBatch.Key);

            CatalogWriter writer = new CatalogWriter(storage, new CatalogContext(), 10, false);
            foreach (JObject item in sortedBatch.Value)
            {
                writer.Add(new RegistrationCatalogItem(item));

                ItemCount++;
            }
            await writer.Commit();
        }

        class RegistrationStorage : Storage
        {
            IDictionary<Uri, StorageContent> _resources = new Dictionary<Uri, StorageContent>();

            public RegistrationStorage() : base(new Uri("http://tempuri.org"))
            {
            }

            public override Task Save(Uri resourceUri, StorageContent content)
            {
                _resources[resourceUri] = content;
                return Task.FromResult(0);
            }

            public override Task<StorageContent> Load(Uri resourceUri)
            {
                throw new NotImplementedException();
            }

            public override Task Delete(Uri resourceUri)
            {
                throw new NotImplementedException();
            }

            public Task SaveTo(Storage storage, string id)
            {
                //string json = Utils.CreateJson(resourceGraph, frame);

                IGraph combinedResourceGraph = new Graph();

                foreach (KeyValuePair<Uri, StorageContent> resource in _resources)
                {
                    Console.WriteLine(resource.Key);
                }

                CatalogContext context = new CatalogContext();

                string json = Utils.CreateJson(combinedResourceGraph, null);

                StorageContent content = new StringStorageContent(
                    json,
                    contentType: "application/json",
                    cacheControl: "public, max-age=300, s-maxage=300");

                return storage.Save(new Uri(storage.BaseAddress, id), content);
            }
        }

        class RegistrationCatalogItem : CatalogItem
        {
            static Uri ItemType = new Uri("http://nuget.org/schema#Package");

            JObject _obj;
            string _version;
            Uri _itemUri;

            public RegistrationCatalogItem(JObject obj)
            {
                _obj = obj;
                _version = obj["nuget:version"].ToString();
                _itemUri = obj["url"].ToObject<Uri>();
            }

            public override StorageContent CreateContent(CatalogContext context)
            {
                return null;
            }

            public override Uri GetItemType()
            {
                return ItemType;
            }

            protected override string GetItemIdentity()
            {
                return _version;
            }

            public override Uri GetItemAddress()
            {
                return _itemUri;
            }

            public override IGraph CreatePageContent(CatalogContext context)
            {
                Uri resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());
                return new Graph();
            }
        }
    }
}
