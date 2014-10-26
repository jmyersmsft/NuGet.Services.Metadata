using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public abstract class CollectorJob : CantonJob
    {
        private CantonCursor _cursor;
        private string _cursorName;

        public CollectorJob(Config config, string cursorName)
            : base(config)
        {
            _cursorName = cursorName;
        }

        public CantonCursor Cursor
        {
            get
            {
                if (_cursor == null)
                {
                    _cursor = new CantonCursor(Account, _cursorName);
                }

                return _cursor;
            }
        }
    }
}
