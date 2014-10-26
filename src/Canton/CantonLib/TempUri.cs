using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public sealed class TempUri
    {
        private readonly Uri _temp;
        private readonly Uri _iri;

        public TempUri(Uri tempLocation, Uri itemIRI)
        {
            _temp = tempLocation;
            _iri = itemIRI;
        }

        public Uri TempLocation
        {
            get
            {
                return _temp;
            }
        }

        public Uri IRI
        {
            get
            {
                return _iri;
            }
        }
    }
}
