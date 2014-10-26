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
    public class CantonCatalogItem : PackageCatalogItem, IDisposable
    {
        private ICloudBlob _blob;
        private Task _task;
        private ManualResetEventSlim _sem;

        // the catalog writer will dipose of this
        private Graph _graph;

        public CantonCatalogItem(ICloudBlob blob)
            : base()
        {
            _blob = blob;
            _sem = new ManualResetEventSlim();
            _task = Task.Run(() => LoadGraph());
        }

        private void LoadGraph()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                _blob.DownloadToStream(stream);

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
