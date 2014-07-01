using System.Collections.Generic;
using System.IO;

namespace CatalogTestTool
{
    interface ILogger
    {
        void Serialize(ReportObject reportObject, StreamWriter writer);//convert ReportObject to Json object
        void LogPackage(string id, string version, List<string> errorMessages, StreamWriter writer);//called by Comparer for each package to log the status of the package
    }
}
