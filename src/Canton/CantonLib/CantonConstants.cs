using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public static class CantonConstants
    {
        /// <summary>
        /// Contains the url of an uploaded nupkg, and the gallery details for it.
        /// </summary>
        public const string GalleryPages = "cantongallerypages";

        /// <summary>
        /// Contains the url of finished catalog pages that need to be added to the index.
        /// </summary>
        public const string CatalogPageQueue = "cantoncatalogpages";

        /// <summary>
        /// Contains finished commits that need registration updates.
        /// </summary>
        public const string CatalogCommitQueue = "cantoncatalogcommits";

        /// <summary>
        /// Canton cursor table.
        /// </summary>
        public const string CursorTable = "cantoncursors";


        public static readonly DateTime MinSupportedDateTime = DateTime.FromFileTimeUtc(0);
    }
}
