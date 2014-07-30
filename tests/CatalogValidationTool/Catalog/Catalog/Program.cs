using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTestTool
{
    public class Program
    {
        public static StreamWriter totalTimeForRun = new StreamWriter(Path.Combine(Environment.CurrentDirectory,"totalTime.txt"));
        static void Main()
        {
            try
            {
                bool createMiniDB = Boolean.Parse(ConfigurationManager.AppSettings["CreateMiniDB"]);
                bool createCatalog = Boolean.Parse(ConfigurationManager.AppSettings["WriteCatalog"]);
                bool populateMiniDB = Boolean.Parse(ConfigurationManager.AppSettings["PopulateMiniDB"]);
                bool compareSourceToMiniDB = Boolean.Parse(ConfigurationManager.AppSettings["Compare"]);
                TasksList(createMiniDB, createCatalog, populateMiniDB, compareSourceToMiniDB);
            }

            catch (Exception e)
            {
                PrintException(e);
            }

        }

        static void PrintException(Exception e)
        {
            //Print the exception
            if (e is AggregateException)
            {
                foreach (Exception ex in ((AggregateException)e).InnerExceptions)
                {
                    PrintException(ex);
                }
            }

            else
            {
                Console.WriteLine("{0} {1}", e.GetType().Name, e.Message);
                Console.WriteLine("{0}", e.StackTrace);
                if (e.InnerException != null)
                {
                    PrintException(e.InnerException);
                }
            }
        }

        public static void TasksList(bool createMiniDB, bool createCatalog, bool populateMiniDB, bool compareSourceToMiniDB)
        {
            //string baseAddress = "http://linked.blob.core.windows.net/demo/"; //"http://localhost:8000/";
            string baseAddress = ConfigurationManager.AppSettings["CatalogAddress"];
            totalTimeForRun.WriteLine(DateTime.Now);

            using (StreamWriter writer = new StreamWriter(Path.Combine(Environment.CurrentDirectory,"Time.txt")))
            {
                if (createMiniDB)
                {
                    writer.WriteLine("Start DataBase and Tables Creation: " + DateTime.Now);
                    CreateTablesMiniDB.CreateDatabaseAndTables();//Creates the miniDB
                    writer.WriteLine("End DataBase and Tables Creation: " + DateTime.Now);
                }

                if (createCatalog)
                {
                    writer.WriteLine("Start Catalog: " + DateTime.Now);
                    TestCatalogWriter.WriteCatalog();//Writes a catalog
                    writer.WriteLine("End Catalog: " + DateTime.Now);
                }

                if (populateMiniDB)
                {
                    writer.WriteLine("Start DB population: " + DateTime.Now);
                    DataBaseGenerator.PopulateDB();//Reads the catalog and populates miniDB
                    writer.WriteLine("End DB population: " + DateTime.Now);
                }

                if (compareSourceToMiniDB)
                {                    
                    writer.WriteLine("Start Comparison: " + DateTime.Now);
                    string connectionStringSource = ConfigurationManager.AppSettings["SourceDBConnectionString"];
                    string connectionStringMiniDB = ConfigurationManager.AppSettings["MiniDBConnectionString"];
                    DBComparer dbComparer = new DBComparer();
                    int packageCount = dbComparer.ValidateDataIntegrity(connectionStringSource, connectionStringMiniDB, totalTimeForRun);//Compare miniDB and source DB- check for data integrity
                    writer.WriteLine("End Comparison: " + DateTime.Now);                   
                    Console.WriteLine("COMPARISON COMPLETE.");
                    Console.WriteLine("Please find the report in the specified directory");
                }                
            }            
        }
    }
}
