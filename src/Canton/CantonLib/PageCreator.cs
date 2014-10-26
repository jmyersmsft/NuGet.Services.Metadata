using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Writing;

namespace NuGet.Canton
{
    public class PageCreator : AppendOnlyCatalogWriter
    {
        protected int _threads = 8;

        public PageCreator(Storage storage)
            : base(storage)
        {

        }

        public override async Task Commit(DateTime commitTimeStamp, IGraph commitMetadata = null)
        {
            if (!_open)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (_batch.Count == 0)
            {
                return;
            }

            //  the commitId is only used for tracing and trouble shooting
            Guid commitId = Guid.NewGuid();

            //  save items
            IDictionary<string, CatalogItemSummary> newItemEntries = await SaveItems(commitId, commitTimeStamp);

            _batch.Clear();
        }

        async Task<IDictionary<string, CatalogItemSummary>> SaveItems(Guid commitId, DateTime commitTimeStamp)
        {
            ConcurrentDictionary<string, CatalogItemSummary> pageItems = new ConcurrentDictionary<string, CatalogItemSummary>();

            ParallelOptions options = new ParallelOptions();
            options.MaxDegreeOfParallelism = _threads;

            var items = _batch.ToArray();

            Parallel.ForEach(items, options, item =>
            {
                Uri resourceUri = null;

                try
                {
                    item.TimeStamp = commitTimeStamp;
                    item.CommitId = commitId;
                    item.BaseAddress = Storage.BaseAddress;

                    Uri catalogPageUri = CreateCatalogPage(item);

                    if (catalogPageUri != null)
                    {
                        Uri indexPageUri = CreateIndexEntry(item, catalogPageUri, commitId, commitTimeStamp);

                        CommitItemComplete(catalogPageUri, indexPageUri);
                    }
                    else
                    {
                        Debug.Fail("Missing catalog content");
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("item uri: {0}", resourceUri), e);
                }
            });

            return pageItems;
        }

        protected virtual Uri CreateCatalogPage(CatalogItem item)
        {
            StorageContent content = item.CreateContent(Context);

            if (content != null)
            {
                var resourceUri = item.GetItemAddress();
                Storage.Save(resourceUri, content).Wait();
                return resourceUri;
            }

            return null;
        }

        protected virtual Uri CreateIndexEntry(CatalogItem item, Uri resourceUri, Guid commitId, DateTime commitTimeStamp)
        {
            IGraph pageContent = item.CreatePageContent(Context);
            AddCatalogEntryData(pageContent, item.GetItemType(), resourceUri, commitId, commitTimeStamp);

            StringBuilder sb = new StringBuilder();
            using (var stringWriter = new System.IO.StringWriter(sb))
            {
                CompressingTurtleWriter turtleWriter = new CompressingTurtleWriter();
                turtleWriter.Save(pageContent, stringWriter);
            }

            StorageContent content = new StringStorageContent(sb.ToString(), "application/json", "no-store");

            Uri tmpUri = GetTempUri("catalogindexpage", "ttl");

            Storage.Save(tmpUri, content).Wait();

            return tmpUri;
        }

        protected Uri GetTempUri(string folder, string extension)
        {
            return new Uri(String.Format(CultureInfo.InvariantCulture, "{0}{1}/{2}.{3}", Storage.BaseAddress.AbsoluteUri, folder, Guid.NewGuid().ToString(), extension).ToLowerInvariant());
        }

        protected virtual void CommitItemComplete(Uri resourceUri, Uri pageUri)
        {
            // this should be overridden
        }

        private void AddCatalogEntryData(IGraph pageContent, Uri itemType, Uri resourceUri, Guid commitId, DateTime commitTimeStamp)
        {
            
            var pageContentRoot = pageContent.CreateUriNode(resourceUri);
            pageContent.Assert(pageContentRoot, pageContent.CreateUriNode(Schema.Predicates.Type), pageContent.CreateUriNode(itemType));
            pageContent.Assert(pageContentRoot, pageContent.CreateUriNode(Schema.Predicates.CatalogCommitId), pageContent.CreateLiteralNode(commitId.ToString()));
            pageContent.Assert(pageContentRoot,
                pageContent.CreateUriNode(Schema.Predicates.CatalogCommitId),
                pageContent.CreateLiteralNode(commitTimeStamp.ToString("O"), Schema.DataTypes.DateTime));
        }

        async Task SaveRoot(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> pageEntries, IGraph commitMetadata)
        {
            await SaveIndexResource(RootUri, Schema.DataTypes.CatalogRoot, commitId, commitTimeStamp, pageEntries, null, commitMetadata, GetAdditionalRootType());
        }
    }
}
