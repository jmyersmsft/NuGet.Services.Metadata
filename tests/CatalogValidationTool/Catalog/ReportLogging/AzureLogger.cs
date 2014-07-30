using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using System.Configuration;

namespace CatalogTestTool
{
    // TODO: Connect to Azure logger and put the logging report on it, instead of on disc
    public class AzureLogger : ILogger
    {      
        public Dictionary<string, List<string>> ReportDictionary = new Dictionary<string, List<string>>();
        /*Input: id,version and error messages about the current package; writer which is the report   
        Calls Serialize method which serializes the reporting objects into JSON*/
        public void LogPackage(string id, string version, bool errorsFound, List<string> errorMessages, StreamWriter writer)
        {
            if (errorsFound)
            {
                ReportObject reportObject = new ReportObject(id, version, errorMessages);
                Serialize(reportObject, writer);                
            }          
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

        /*Called by DBComparer after ReportDictionary is populated with errors and id,version of the packages with errors */
        public void HtmlRender(int packageCount, StreamWriter totalTimeForRun)
        {
            int errors = ReportDictionary.Count;
            totalTimeForRun.WriteLine(DateTime.Now);
            totalTimeForRun.Close();
            StreamReader reader = new StreamReader(Path.Combine(Environment.CurrentDirectory, "totalTime.txt"));
            DateTime start = Convert.ToDateTime(reader.ReadLine());
            DateTime end = Convert.ToDateTime(reader.ReadLine());
            StringBuilder resultOverview = new StringBuilder();
            string testResultPath = ConfigurationManager.AppSettings["HTMLReport"];
            HTMLLogger logger = new HTMLLogger();
            logger.WriteTitle("Comparison Report for {0} packages",packageCount);
            logger.WriteHeader("Error log for {0} packages",errors);
            logger.WriteSubHeader("Total time: {0}", end.Subtract(start));       
            logger.WriteTable(ReportDictionary);
            StreamWriter writer = new StreamWriter(testResultPath);
            writer.Write(logger.stringwriter.ToString());
            writer.Close();
        }

    }
}
