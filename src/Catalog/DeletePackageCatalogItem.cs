using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using VDS.RDF;
using VDS.RDF.Parsing;
using System.Text;
using System.IO;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog
{
    public class DeletePackageCatalogItem : AppendOnlyCatalogItem
    {

        protected string _id;
        protected string _version;
        protected string _reason;
        protected string _initiatedBy;
        protected DateTime _deleteTimeStamp;

        #region strings
        //Strings needed for Item Identity
        private const string delete = "Delete";

        //String needed for Json content
        private const string catalogNameSpace = "catalog:";
        private const string commitTimeStamp = "commitTimeStamp";
        private const string deleteTimeStamp = "deleteTimeStamp";
        #endregion

        public DeletePackageCatalogItem(string id, NuGetVersion version, string reason, string initiatedBy)
        {
            _version = (version == null) ? string.Empty : version.ToNormalizedString();
            _id = id;
            _reason = reason;
            _initiatedBy = initiatedBy;
            _deleteTimeStamp = DateTime.UtcNow;
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            var json = new JObject();
            Uri resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());
            json.Add("@id", resourceUri);

            JArray array = new JArray();
            array.Add(GetItemType().ToString().Split('#')[1]);
            array.Add(catalogNameSpace + "Permalink");
            json["@type"] = array;

            json.Add("id", _id);

            if (!String.IsNullOrEmpty(_version))
            {
                json.Add("version", _version);

            }
            json.Add(deleteTimeStamp, _deleteTimeStamp.ToString("O"));
            json.Add("initiatedBy", _initiatedBy);
            json.Add("reason", _reason);
            json.Add(catalogNameSpace + commitTimeStamp, TimeStamp);

            var contextcontent = new JObject{
                {"@vocab", "http://schema.nuget.org/schema#"},
                {"catalog", "http://schema.nuget.org/catalog#"},
                {catalogNameSpace + commitTimeStamp, new JObject{{"@type", "http://www.w3.org/2001/XMLSchema#dateTime"}}},
                {deleteTimeStamp, new JObject{{"@type",  "http://www.w3.org/2001/XMLSchema#dateTime"}}}
            };

            json.Add("@context", contextcontent);

            StorageContent content = new StringStorageContent(json.ToString(), "application/json", "no-store");
            return content;
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.PackageDelete;
        }

        protected override string GetItemIdentity()
        {
            string filename = String.IsNullOrEmpty(_version) ? _id + "." + delete : _id + "." + _version + "." + delete;
            return filename.ToLowerInvariant();
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            Uri resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());

            Graph graph = new Graph();

            INode subject = graph.CreateUriNode(resourceUri);
            INode idPredicate = graph.CreateUriNode(Schema.Predicates.Id);
            INode versionPredicate = graph.CreateUriNode(Schema.Predicates.Version);

            if (_id != null)
            {
                graph.Assert(subject, idPredicate, graph.CreateLiteralNode(_id));
            }

            if (_version != null)
            {
                graph.Assert(subject, versionPredicate, graph.CreateLiteralNode(_version));
            }

            return graph;
        }
    }
}
