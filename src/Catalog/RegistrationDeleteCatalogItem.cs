using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Linq;
using VDS.RDF;
using VDS.RDF.Query;
using NuGet.Versioning;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class RegistrationDeleteCatalogItem : CatalogItem
    {
        Uri _catalogUri;
        IGraph _catalogItem;
        Uri _itemAddress;
        Uri _packageContentBaseAddress;
        Uri _registrationBaseAddress;
        public NuGetVersion _versionToDelete;

        public RegistrationDeleteCatalogItem(Uri catalogUri, IGraph catalogItem, Uri packageContentBaseAddress, Uri registrationBaseAddress)
        {
            _catalogUri = catalogUri;
            _catalogItem = catalogItem;
            _packageContentBaseAddress = packageContentBaseAddress;
            _registrationBaseAddress = registrationBaseAddress;
            var task = GetPackageVersionForDelete(catalogUri, new CollectorHttpClient());
            task.Wait();
            _versionToDelete = task.Result;
        }

        protected async static Task<NuGetVersion> GetPackageVersionForDelete(Uri packageUri, CollectorHttpClient httpClient)
        {
            JObject jsonContent = await httpClient.GetJObjectAsync(packageUri);
            if (jsonContent["version"] != null)
            {
                return NuGetVersion.Parse(jsonContent["version"].ToString());
            }
            else
            {
                return null;
            }
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.PackageDelete;
        }

        public override Uri GetItemAddress()
        {
            if (_itemAddress == null)
            {
                INode subject = _catalogItem.CreateUriNode(_catalogUri);
                string version = _catalogItem.GetTriplesWithSubjectPredicate(subject, _catalogItem.CreateUriNode(Schema.Predicates.Version)).FirstOrDefault().Object.ToString().ToLowerInvariant();
                _itemAddress = new Uri(BaseAddress, version + ".json");
            }

            return _itemAddress;
        }
    }
}
