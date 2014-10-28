using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Canton.jobs
{
    public class QueueRegBatchesJob : CollectorJob
    {
        private readonly CollectorHttpClient _httpClient;

        public QueueRegBatchesJob(Config config, Storage storage)
            : base(config, storage, "queueregbatches")
        {
            _httpClient = new CollectorHttpClient();
        }

        public override async Task RunCore()
        {
            DateTime position = Cursor.Position;

            // Get the catalog index
            Uri catalogIndexUri = new Uri(Config.GetProperty("CatalogIndex"));

            JObject index = await _httpClient.GetJObjectAsync(catalogIndexUri);

            List<Tuple<DateTime, Uri>> pages = new List<Tuple<DateTime,Uri>>();

            foreach (var item in index["items"])
            {
                pages.Add(new Tuple<DateTime, Uri>(DateTime.Parse(item["commitTimeStamp"].ToString()), new Uri(item["@id"].ToString())));
            }

            pages.Sort(SortPages);

            var newPages = pages.Where(p => p.Item1.CompareTo(position) > 0).ToList();

            if (newPages.Count > 0)
            {
                var jsonPages = GetPages(pages.Select(t => t.Item2));

                ConcurrentDictionary<string, ConcurrentQueue<Uri>> batches = new ConcurrentDictionary<string, ConcurrentQueue<Uri>>(StringComparer.OrdinalIgnoreCase);

                foreach (var newPage in newPages)
                {
                    //foreach (var item in newPage.["items"])
                    //{
                    //    string id = item["nuget:id"].ToString();
                    //}
                }

            }
        }

        private ConcurrentDictionary<Uri, JObject> GetPages(IEnumerable<Uri> uris)
        {
            ConcurrentDictionary<Uri, JObject> pages = new ConcurrentDictionary<Uri, JObject>();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 8;

            Parallel.ForEach(uris.ToArray(), options, uri =>
            {
                var task = _httpClient.GetJObjectAsync(uri);
                task.Wait();

                pages.TryAdd(uri, task.Result);
            });

            return pages;
        }

        private static int SortPages(Tuple<DateTime, Uri> x, Tuple<DateTime, Uri> y)
        {
            return x.Item1.CompareTo(y.Item1);
        }
    }
}
