using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;

namespace CatalogTestTool
{
    // TODO: Connect to Azure logger and put the logging report on it, instead of on disc
    public class AzureLogger : ILogger
    {
        /*Input: id,version and error messages about the current package; writer which is the report   
        Calls Serialize method which serializes the reporting objects into JSON*/
        public void LogPackage(string id, string version, List<string> errorMessages, StreamWriter writer)
        {
            ReportObject reportObject = new ReportObject(id, version, errorMessages);
            Serialize(reportObject, writer);
        }

        /*Input: reporting object and the report
         Logs the status of the package on the JSON file*/
        public void Serialize(ReportObject reportObject, StreamWriter writer)
        {
            MemoryStream stream = new MemoryStream();
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(ReportObject));
            serializer.WriteObject(stream, reportObject);
            stream.Position = 0;
            StreamReader streamReader = new StreamReader(stream);
            writer.WriteLine(streamReader.ReadToEnd());
            writer.WriteLine();
            stream.Close();
        }

    }
}
