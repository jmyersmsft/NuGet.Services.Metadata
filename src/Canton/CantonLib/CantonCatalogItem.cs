using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Maintenance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class CantonCatalogItem : AppendOnlyCatalogItem
    {


        public override Uri GetItemType()
        {
            return Schema.DataTypes.PackageDetails;
        }
    }
}
