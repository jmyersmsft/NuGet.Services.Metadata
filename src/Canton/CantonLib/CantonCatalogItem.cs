using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace NuGet.Canton
{
    /// <summary>
    /// Background loading pre-built page
    /// </summary>
    public class CantonCatalogItem : PackageCatalogItem, IDisposable
    {
        private ICloudBlob _blob;
        private Task _task;
        private ManualResetEventSlim _sem;
        private CloudStorageAccount _account;
        private Uri _uri;

        // the catalog writer will dipose of this
        private Graph _graph;

        public CantonCatalogItem(CloudStorageAccount account, Uri uri)
            : base()
        {
            _uri = uri;
            _account = account;
            _sem = new ManualResetEventSlim();
            _task = Task.Run(() => LoadGraph());
        }

        private void LoadGraph()
        {
            var client = _account.CreateCloudBlobClient();
            _blob = client.GetBlobReferenceFromServer(_uri);

            using (MemoryStream stream = new MemoryStream())
            {
                _blob.DownloadToStream(stream);
                stream.Seek(0, SeekOrigin.Begin);

                _graph = new Graph();

                using (StreamReader reader = new StreamReader(stream))
                {
                    TurtleParser parser = new TurtleParser();
                    parser.Load(_graph, reader);
                }
            }

            SetIdVersionFromGraph(_graph);

            _sem.Set();
        }

        public async Task DeleteBlob()
        {
            _sem.Wait();

            await _blob.DeleteIfExistsAsync();
        }

        protected override XDocument GetNuspec()
        {
            throw new NotImplementedException();
        }

        public override IGraph CreateContentGraph(CatalogContext context)
        {
            _sem.Wait();

            INode rdfTypePredicate = _graph.CreateUriNode(Schema.Predicates.Type);
            Triple resource = _graph.GetTriplesWithPredicateObject(rdfTypePredicate, _graph.CreateUriNode(GetItemType())).First();

            Uri oldUri = ((IUriNode)resource.Subject).Uri;
            Uri newUri = GetItemAddress();

            CantonUtilities.ReplaceIRI(_graph, oldUri, newUri);

            return _graph;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            _sem.Wait();

            return base.CreatePageContent(context);
        }

        public void Dispose()
        {
            _task.Dispose();
            _sem.Dispose();
        }
    }
}
