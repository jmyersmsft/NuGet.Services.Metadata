using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class MemoryStorage : Storage
    {
        IDictionary<string, StorageContent> _store;

        public MemoryStorage(IDictionary<string, StorageContent> store) 
            : base(new Uri("http://tempuri.org/memory"))
        {
            _store = store;
        }

        public override Task Save(Uri resourceUri, StorageContent content)
        {
            _store[resourceUri.ToString()] = content;
            return Task.FromResult(0);
        }

        public override Task<StorageContent> Load(Uri resourceUri)
        {
            StorageContent content;
            if (!_store.TryGetValue(resourceUri.ToString(), out content))
            {
                content = null;
            }
            return Task.FromResult(content);
        }

        public override Task Delete(Uri resourceUri)
        {
            _store.Remove(resourceUri.ToString());
            return Task.FromResult(0);
        }
    }
}
