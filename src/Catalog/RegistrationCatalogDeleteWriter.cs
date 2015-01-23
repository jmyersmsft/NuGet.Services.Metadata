using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;
using System.IO;
using Newtonsoft.Json;  

namespace NuGet.Services.Metadata.Catalog
{
    public class RegistrationCatalogDeleteWriter : CatalogWriterBase
    {
        IList<Uri> _cleanUpList;
        private bool allVersionsDelete = false;
        
        public RegistrationCatalogDeleteWriter(Storage storage, int partitionSize = 100, IList<Uri> cleanUpList = null, ICatalogGraphPersistence graphPersistence = null, CatalogContext context = null)
            :base(storage, graphPersistence, context)
        {
            _cleanUpList = cleanUpList;
            PartitionSize = partitionSize;
        }
        public int PartitionSize { get; private set; }
       
        protected override IDictionary<string, CatalogItemSummary> SaveItems(Guid commitId, DateTime commitTimeStamp)
        {
            Dictionary<string, CatalogItemSummary> pageItems = new Dictionary<string, CatalogItemSummary>();
            int batchIndex = 0;

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 8;

            List<RegistrationDeleteCatalogItem> items = _batch.Cast<RegistrationDeleteCatalogItem>().ToList();

            Parallel.ForEach(items, options, item =>
            {
                Uri resourceUri = null;

                try
                {
                    item.TimeStamp = commitTimeStamp;
                    item.CommitId = commitId;
                    item.BaseAddress = Storage.BaseAddress;

                    if (item._versionToDelete != null)
                    {
                        resourceUri = item.GetItemAddress();

                        //Delete a specific version
                        Task deleteTask = null;
                        deleteTask = Storage.Delete(resourceUri);
                        deleteTask.Wait();
                    }
                    else
                    {
                        //Delete all versions
                        Task deleteTask = null;
                        deleteTask = Storage.Delete(item.BaseAddress);
                        deleteTask.Wait();
                        allVersionsDelete = true;

                    }
                    batchIndex++;
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("item uri: {0} batch index: {1}", resourceUri, batchIndex), e);
                }
            });
            return pageItems;
        }

        protected override async Task<IDictionary<string, CatalogItemSummary>> SavePages(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> itemEntries, bool largeToSmall = false)
        {
            if (allVersionsDelete)
            {
                return null;
            }

            //Only version of the package and index.json are already deleted case
            string indexJson = await Storage.LoadString(RootUri);
            if (String.IsNullOrEmpty(indexJson))
            {
                return null;
            }
            
            SortedDictionary<NuGetVersion, KeyValuePair<string, CatalogItemSummary>> versions = new SortedDictionary<NuGetVersion, KeyValuePair<string, CatalogItemSummary>>();

            //  load items from existing pages

            IDictionary<string, CatalogItemSummary> pageEntries = await LoadIndexResource(RootUri, largeToSmall);

            foreach (KeyValuePair<string, CatalogItemSummary> pageEntry in pageEntries)
            {
                IDictionary<string, CatalogItemSummary> pageItemEntries = await LoadIndexResource(new Uri(pageEntry.Key), largeToSmall);

                foreach (KeyValuePair<string, CatalogItemSummary> pageItemEntry in pageItemEntries)
                {
                    NuGetVersion version = GetPackageVersion(new Uri(pageItemEntry.Key), pageItemEntry.Value.Content);
                    versions.Add(version, pageItemEntry);
                }
            }

            //  delete new items
            List<CatalogItem> itemsToDelete = new List<CatalogItem>();
            if (_batch != null)
            {
                itemsToDelete = _batch.ToList();
            }

            foreach (RegistrationDeleteCatalogItem item in itemsToDelete)
            {
                NuGetVersion versionToRemove = item._versionToDelete;
                if (versionToRemove == null)
                {
                    //Remove all versions
                    versions.Clear();
                }
                else
                {
                    //Remove the specified version
                    versions.Remove(versionToRemove);
                }
            }

            //  (re)create pages
            IDictionary<string, CatalogItemSummary> newPageEntries = await PartitionAndSavePages(commitId, commitTimeStamp, versions);

            //  add to list of pages to clean up

            if (_cleanUpList != null)
            {
                foreach (string existingPage in pageEntries.Keys)
                {
                    if (!newPageEntries.ContainsKey(existingPage))
                    {
                        _cleanUpList.Add(new Uri(existingPage));
                    }
                }
            }

            return newPageEntries;
        }

        protected override Uri[] GetAdditionalRootType()
        {
            return new Uri[] { Schema.DataTypes.PackageRegistration, Schema.DataTypes.Permalink };
        }

        protected async Task<IDictionary<string, CatalogItemSummary>> PartitionAndSavePages(Guid commitId, DateTime commitTimeStamp, SortedDictionary<NuGetVersion, KeyValuePair<string, CatalogItemSummary>> versions)
        {
            IDictionary<string, CatalogItemSummary> newPageEntries = new Dictionary<string, CatalogItemSummary>();

            foreach (IEnumerable<KeyValuePair<NuGetVersion, KeyValuePair<string, CatalogItemSummary>>> partition in Utils.Partition(versions, PartitionSize))
            {
                string lower = partition.First().Key.ToString();
                string upper = partition.Last().Key.ToString();
                string relativeAddress = "page/" + lower + "/" + upper.ToLowerInvariant();
                Uri newPageUri;

                newPageUri = CreatePageUri(Storage.BaseAddress, relativeAddress);
                           
                IDictionary<string, CatalogItemSummary> newPageItemEntries = new Dictionary<string, CatalogItemSummary>();
                foreach (KeyValuePair<NuGetVersion, KeyValuePair<string, CatalogItemSummary>> version in partition)
                {
                    newPageItemEntries.Add(version.Value);
                }

                IGraph extra = CreateExtraGraph(newPageUri, lower, upper);

                await SaveIndexResource(newPageUri, Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, newPageItemEntries, RootUri, extra, null);
                
                newPageEntries[newPageUri.AbsoluteUri] = new CatalogItemSummary(Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, newPageItemEntries.Count, CreatePageSummary(newPageUri, lower, upper));
            }

            return newPageEntries;
        }

        protected override async Task SaveRoot(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> pageEntries, IGraph commitMetadata)
        {
            await SaveIndexResource(RootUri, Schema.DataTypes.CatalogRoot, commitId, commitTimeStamp, pageEntries, null, commitMetadata, GetAdditionalRootType());
        }

        protected static IGraph CreateExtraGraph(Uri pageUri, string lower, string upper)
        {
            IGraph graph = new Graph();
            INode resourceNode = graph.CreateUriNode(pageUri);
            graph.Assert(resourceNode, graph.CreateUriNode(Schema.Predicates.Lower), graph.CreateLiteralNode(lower));
            graph.Assert(resourceNode, graph.CreateUriNode(Schema.Predicates.Upper), graph.CreateLiteralNode(upper));
            return graph;
        }

        protected static NuGetVersion GetPackageVersion(Uri packageUri, IGraph pageContent)
        {
            Triple t1 = pageContent.GetTriplesWithSubjectPredicate(
                pageContent.CreateUriNode(packageUri),
                pageContent.CreateUriNode(Schema.Predicates.CatalogEntry)).FirstOrDefault();

            Triple t2 = pageContent.GetTriplesWithSubjectPredicate(
                pageContent.CreateUriNode(((IUriNode)t1.Object).Uri),
                pageContent.CreateUriNode(Schema.Predicates.Version)).FirstOrDefault();

            string s = t2.Object.ToString();
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }
            return NuGetVersion.Parse(s);
        }

        protected static IGraph CreatePageSummary(Uri newPageUri, string lower, string upper)
        {
            IGraph graph = new Graph();

            INode resourceUri = graph.CreateUriNode(newPageUri);

            graph.Assert(resourceUri, graph.CreateUriNode(Schema.Predicates.Lower), graph.CreateLiteralNode(lower));
            graph.Assert(resourceUri, graph.CreateUriNode(Schema.Predicates.Upper), graph.CreateLiteralNode(upper));

            return graph;
        }

        protected override StorageContent CreateIndexContent(IGraph graph, Uri type)
        {
            JObject frame = Context.GetJsonLdContext("context.Registration.json", type);
            return new StringStorageContent(Utils.CreateJson(graph, frame), "application/json", "no-store");
        }
     }
}
