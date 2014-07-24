using JsonLD.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using System.IO;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Configuration;

namespace CatalogTestTool
{

    public class PackageUriCollector : Collector
    {

     
     
        int _batchSize;

        public PackageUriCollector(int batchSize)
        {
            _batchSize = batchSize;
            DictionaryOfPackages = new Dictionary<Tuple<string, string>, string>();
        }

        public int PageCount
        {
            private set;
            get;
        }

        public Dictionary<Tuple<string, string>, string> DictionaryOfPackages
        {
            private set;
            get;
        }

        protected override async Task<CollectorCursor> Fetch(CollectorHttpClient client, Uri index, CollectorCursor last)
        {
            CollectorCursor cursor = last;
            DateTime lastDateTime = (DateTime)last;

            IList<JObject> items = new List<JObject>();

            JObject root = await client.GetJObjectAsync(index);

            JToken context = null;
            root.TryGetValue("@context", out context);

            IEnumerable<JToken> rootItems = root["items"].OrderBy(item => item["commitTimestamp"].ToObject<DateTime>());

            foreach (JObject rootItem in rootItems)
            {
                DateTime pageTimeStamp = rootItem["commitTimestamp"].ToObject<DateTime>();

                if (pageTimeStamp > lastDateTime)
                {
                    Uri pageUri = rootItem["url"].ToObject<Uri>();
                    JObject page = await client.GetJObjectAsync(pageUri);

                    IEnumerable<JToken> pageItems = page["items"].OrderBy(item => item["commitTimestamp"].ToObject<DateTime>());

                    foreach (JObject pageItem in pageItems)
                    {
                        DateTime itemTimeStamp = pageItem["commitTimestamp"].ToObject<DateTime>();

                        if (itemTimeStamp > lastDateTime)
                        {
                            cursor = itemTimeStamp;

                            Uri itemUri = pageItem["url"].ToObject<Uri>();

                            items.Add(pageItem);

                            if (items.Count == _batchSize)
                            {
                                await ProcessBatch(client, items, (JObject)context);
                                PageCount++;
                                items.Clear();
                            }
                        }
                    }
                }
            }

            if (items.Count > 0)
            {
                await ProcessBatch(client, items, (JObject)context);
                PageCount++;
            }

            return cursor;
        }

        


        protected Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {

            try
            {
                foreach (var item in items)
                {
                    string type = item.Value<string>("@type");
                    string url = item.Value<string>("url");
                    var id = item.Value<string>("nuget:id");
                    var version = item.Value<string>("nuget:version");
                    var key = Int32.Parse(item.Value<string>("galleryKey"));
                    Tuple<string, string> dictKey = new Tuple<string, string>(id, version);
                    if (String.Equals(type, "nuget:Package", StringComparison.Ordinal))//add
                    {
                        DictionaryOfPackages.Add(dictKey, url);
                    }

                    else if (String.Equals(type, "nuget:PackageDeletion", StringComparison.Ordinal))//delete
                    {
                        if (DictionaryOfPackages.ContainsKey(dictKey))
                        {
                            DictionaryOfPackages.Remove(dictKey);
                        }

                    }

                    else if (DictionaryOfPackages.ContainsKey(dictKey))//edit
                    {
                        DictionaryOfPackages[dictKey] = url;
                    }

                }
            }

            catch
            {
                throw;
            }

            using (StreamWriter packages = new StreamWriter(ConfigurationManager.AppSettings["PackagesFromCollector"]))
            {
                foreach (KeyValuePair<Tuple<string, string>, string> package in DictionaryOfPackages)
                {
                    packages.WriteLine(package.Key + " " + package.Value);
                }
            }
            
            return Task.FromResult(0);
        }
    }
}