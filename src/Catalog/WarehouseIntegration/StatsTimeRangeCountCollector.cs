using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.WarehouseIntegration
{
    public class StatsTimeRangeCountCollector : StatsCountCollector
    {
        DateTime MinDownloadTimestamp { get; set; }
        DateTime MaxDownloadTimestamp { get; set; }
        public StatsTimeRangeCountCollector(DateTime minDownloadTimestamp, DateTime maxDownloadTimestamp)
        {
            if(maxDownloadTimestamp < minDownloadTimestamp)
            {
                throw new ArgumentException("maxDownloadTimestamp should be greater than or equal to minDownloadTimestamp");
            }
            MinDownloadTimestamp = minDownloadTimestamp.ToUniversalTime();
            MaxDownloadTimestamp = maxDownloadTimestamp.ToUniversalTime();
        }
        protected override bool SelectItem(DateTime itemMinDownloadTimestamp, DateTime itemMaxDownloadTimestamp)
        {
            return !(MinDownloadTimestamp > itemMaxDownloadTimestamp || MaxDownloadTimestamp < itemMinDownloadTimestamp);
        }

        protected override bool SelectRow(DateTime rowDownloadTimestamp)
        {
            return rowDownloadTimestamp >= MinDownloadTimestamp && rowDownloadTimestamp < MaxDownloadTimestamp;
        }
    }
}
