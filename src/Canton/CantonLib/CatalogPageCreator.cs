using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public class CatalogPageCreator : PageCreator
    {
        private Action<Uri, Uri> _itemComplete;

        public CatalogPageCreator(Storage storage, Action<Uri, Uri> itemComplete)
            : base(storage)
        {
            _itemComplete = itemComplete;
            _threads = 8;
        }

        protected override void CommitItemComplete(Uri resourceUri, Uri pageUri)
        {
            _itemComplete(resourceUri, pageUri);
        }
    }
}
