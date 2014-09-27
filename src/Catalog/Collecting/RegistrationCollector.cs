using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Writing;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCollector : StoreCollector
    {
        Storage _storage;
        JObject _registrationFrame;
        JObject _rangePackagesFrame;

        public string GalleryBaseAddress { get; set; }
        public string ContentBaseAddress { get; set; }

        public int LargeRegistrationThreshold { get; set; }

        public RegistrationCollector(Storage storage, int batchSize)
            : base(batchSize, new Uri[] { Schema.DataTypes.Package })
        {
            _registrationFrame = JObject.Parse(Utils.GetResource("context.Registration.json"));
            _registrationFrame["@type"] = "PackageRegistration";

            _rangePackagesFrame = JObject.Parse(Utils.GetResource("context.Registration.json"));
            _rangePackagesFrame["@type"] = "RangePackages";
            
            _storage = storage;

            GalleryBaseAddress = "http://tempuri.org";
            ContentBaseAddress = "http://tempuri.org";
            LargeRegistrationThreshold = 100;
        }

        protected override async Task ProcessStore(TripleStore store)
        {
            IDictionary<string, SortedSet<NuGetVersion>> packages = GetPackages(store);

            foreach (KeyValuePair<string, SortedSet<NuGetVersion>> registration in packages)
            {
                await ProcessRegistration(store, registration.Key, registration.Value);
            }
        }

        async Task ProcessRegistration(TripleStore store, string id, SortedSet<NuGetVersion> versions)
        {
            Uri registrationUri = CreateRegistrationUri(id);

            IGraph registrationGraph = await LoadRegistration(registrationUri);

            if (registrationGraph == null)
            {
                registrationGraph = CreateRegistrationGraph(registrationUri, id);
            }

            IGraph packagesGraph = CreatePackagesGraph(store, id);

            registrationGraph.Merge(packagesGraph, true);

            IDictionary<string, Uri> packageUriLookup = CreatePackageUriLookup(packagesGraph);

            AddRangesToRegistration(registrationUri, registrationGraph, versions, packageUriLookup);

            await SaveRegistration(registrationUri, registrationGraph);
        }

        async Task<IGraph> LoadRegistration(Uri registrationUri)
        {
            Console.WriteLine("load: {0}", registrationUri);

            string registrationJson = await _storage.LoadString(registrationUri);
            if (registrationJson == null)
            {
                return null;
            }

            IGraph registrationGraph = Utils.CreateGraph(registrationJson);

            //await LoadRangePackages(registrationUri, registrationGraph);

            return registrationGraph;
        }

        async Task SaveRegistration(Uri registrationUri, IGraph registrationGraph)
        {
            int count = registrationGraph.GetTriplesWithPredicateObject(
                registrationGraph.CreateUriNode(Schema.Predicates.Type),
                registrationGraph.CreateUriNode(Schema.DataTypes.Package)).Count();

            if (count < LargeRegistrationThreshold)
            {
                await Save(registrationUri, registrationGraph, _registrationFrame);
            }
            else
            {
                IDictionary<Uri, IGraph> resources = GraphSplitting.Split(registrationUri, registrationGraph);

                IList<Task> tasks = new List<Task>();
                foreach (KeyValuePair<Uri, IGraph> resource in resources)
                {
                    tasks.Add(Save(resource.Key, resource.Value, _registrationFrame));
                }
                Task.WaitAll(tasks.ToArray());
            }
        }

        async Task Save(Uri resourceUri, IGraph resourceGraph, JObject frame)
        {
            string json = Utils.CreateJson(resourceGraph, frame);

            StorageContent content = new StringStorageContent(
                json,
                contentType: "application/json",
                cacheControl: "public, max-age=300, s-maxage=300");

            Console.WriteLine("save: {0}", resourceUri);

            await _storage.Save(resourceUri, content);
        }

        static int CountPackages(IGraph graph)
        {
            return graph.GetTriplesWithPredicateObject(
                graph.CreateUriNode(Schema.Predicates.Type),
                graph.CreateUriNode(Schema.DataTypes.Package)).Count();
        }

        void AddRangesToRegistration(Uri registrationUri, IGraph registrationGraph, SortedSet<NuGetVersion> versions, IDictionary<string, Uri> packageUriLookup)
        {
            IList<Range> ranges = new List<Range>();

            foreach (IEnumerable<NuGetVersion> partition in Utils.Partition(versions, 10))
            {
                IList<Uri> packageUris = new List<Uri>();
                foreach (NuGetVersion version in partition)
                {
                    packageUris.Add(packageUriLookup[version.ToString()]);
                }
                ranges.Add(new Range { Low = partition.First().ToString(), High = partition.Last().ToString(), Packages = packageUris });
            }

            foreach (Range range in ranges)
            {
                Uri rangeUri = new Uri((registrationUri.ToString() + "#range/" + range.Low + "/" + range.High).ToLowerInvariant());
                Uri rangePackagesUri = new Uri((registrationUri.ToString() + "#range/" + range.Low + "/" + range.High + "/packages").ToLowerInvariant());

                INode packageRangeNode = registrationGraph.CreateUriNode(rangeUri);

                registrationGraph.Assert(registrationGraph.CreateUriNode(registrationUri), registrationGraph.CreateUriNode(Schema.Predicates.PackageRange), packageRangeNode);
                registrationGraph.Assert(packageRangeNode, registrationGraph.CreateUriNode(Schema.Predicates.Type), registrationGraph.CreateUriNode(Schema.DataTypes.PackageRange));
                registrationGraph.Assert(packageRangeNode, registrationGraph.CreateUriNode(Schema.Predicates.Low), registrationGraph.CreateLiteralNode(range.Low));
                registrationGraph.Assert(packageRangeNode, registrationGraph.CreateUriNode(Schema.Predicates.High), registrationGraph.CreateLiteralNode(range.High));

                INode packageListNode = registrationGraph.CreateUriNode(rangePackagesUri);

                registrationGraph.Assert(packageRangeNode, registrationGraph.CreateUriNode(Schema.Predicates.PackageList), packageListNode);

                registrationGraph.Assert(packageListNode, registrationGraph.CreateUriNode(Schema.Predicates.Type), registrationGraph.CreateUriNode(Schema.DataTypes.PackageList));

                foreach (Uri package in range.Packages)
                {
                    registrationGraph.Assert(packageListNode, registrationGraph.CreateUriNode(Schema.Predicates.Package), registrationGraph.CreateUriNode(package));
                }
            }
        }

        Uri CreateRegistrationUri(string id)
        {
            string baseAddress = _storage.BaseAddress.ToString();
            Uri resourceUri = new Uri(baseAddress + id + ".json");
            return resourceUri;
        }

        IGraph CreateRegistrationGraph(Uri registrationUri, string id)
        {
            IGraph graph = new Graph();
            INode resource = graph.CreateUriNode(registrationUri);
            graph.Assert(resource, graph.CreateUriNode(Schema.Predicates.Type), graph.CreateUriNode(Schema.DataTypes.PackageRegistration));
            graph.Assert(resource, graph.CreateUriNode(Schema.Predicates.Id), graph.CreateLiteralNode(id));
            return graph;
        }

        IDictionary<string, Uri> CreatePackageUriLookup(IGraph packagesGraph)
        {
            IDictionary<string, Uri> result = new Dictionary<string, Uri>();
            foreach (KeyValuePair<string, Uri> item in packagesGraph.GetTriplesWithPredicate(Schema.Predicates.Version).Select((t) => new KeyValuePair<string, Uri>(((ILiteralNode)t.Object).Value, ((IUriNode)t.Subject).Uri)))
            {
                result.Add(item);
            }
            return result;
        }

        IGraph CreatePackagesGraph(TripleStore store, string id)
        {
            SparqlParameterizedString sparql = new SparqlParameterizedString();
            sparql.CommandText = Utils.GetResource("sparql.ConstructPackageGraph.rq");

            string baseAddress = _storage.BaseAddress.ToString();

            sparql.SetLiteral("id", id);
            sparql.SetLiteral("base", baseAddress);
            sparql.SetLiteral("extension", ".json");
            sparql.SetLiteral("galleryBase", GalleryBaseAddress);
            sparql.SetLiteral("contentBase", ContentBaseAddress);

            IGraph graph = SparqlHelpers.Construct(store, sparql.ToString());

            return graph;
        }

        static IDictionary<string, SortedSet<NuGetVersion>> GetPackages(TripleStore store)
        {
            SparqlResultSet rows = SparqlHelpers.Select(store, Utils.GetResource("sparql.SelectPackages.rq"));

            IDictionary<string, SortedSet<NuGetVersion>> packages = new Dictionary<string, SortedSet<NuGetVersion>>();

            foreach (SparqlResult row in rows)
            {
                string id = row["id"].ToString();
                NuGetVersion version = NuGetVersion.Parse(row["version"].ToString());

                SortedSet<NuGetVersion> versions;
                if (!packages.TryGetValue(id, out versions))
                {
                    versions = new SortedSet<NuGetVersion>();
                    packages.Add(id, versions);
                }

                versions.Add(version);
            }

            return packages;
        }

        class Range
        {
            public string Low { get; set; }
            public string High { get; set; }
            public IList<Uri> Packages { get; set; }
        }
    }
}
