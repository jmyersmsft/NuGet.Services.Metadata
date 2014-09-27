using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class InterceptStorage : Storage
    {
        Storage _innerStorage;

        public InterceptStorage(Storage innerStorage) 
            : base(innerStorage.BaseAddress)
        {
            _innerStorage = innerStorage;
        }

        public override Task Save(Uri resourceUri, StorageContent content)
        {
            Console.WriteLine("Save: {0}", resourceUri);

            return _innerStorage.Save(resourceUri, content);
        }

        public override Task<StorageContent> Load(Uri resourceUri)
        {
            Console.WriteLine("Load: {0}", resourceUri);

            return _innerStorage.Load(resourceUri);
        }

        public override Task Delete(Uri resourceUri)
        {
            Console.WriteLine("Delete: {0}", resourceUri);

            return _innerStorage.Delete(resourceUri);
        }
    }
}
