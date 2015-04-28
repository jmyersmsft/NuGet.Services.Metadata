﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class SortingGraphCollector : SortingCollector
    {
        Uri[] _types;

        public SortingGraphCollector(Uri index, Uri[] types, Func<HttpMessageHandler> handlerFunc = null)
            : base(index, handlerFunc)
        {
            _types = types;
        }

        protected override async Task ProcessSortedBatch(CollectorHttpClient client, KeyValuePair<string, IList<JObject>> sortedBatch, JToken context)
        {
            IDictionary<string, IGraph> graphs = new Dictionary<string, IGraph>();

            foreach (JObject item in sortedBatch.Value)
            {
                if (Utils.IsType((JObject)context, item, _types))
                {
                    string itemUri = item["@id"].ToString();
                    IGraph graph = await client.GetGraphAsync(new Uri(itemUri));
                    graphs.Add(itemUri, graph);
                }
            }

            if (graphs.Count > 0)
            {
                await ProcessGraphs(client, new KeyValuePair<string, IDictionary<string, IGraph>>(sortedBatch.Key, graphs));
            }
        }

        protected abstract Task ProcessGraphs(CollectorHttpClient client, KeyValuePair<string, IDictionary<string, IGraph>> sortedGraphs);
    }
}
