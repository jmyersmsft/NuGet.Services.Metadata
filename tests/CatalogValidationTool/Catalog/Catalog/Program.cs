using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTestTool
{
    class Program
    {

        static void Main()
        {
            try
            {
                bool createMiniDB = true;
                bool createCatalog = false;
                bool populateMiniDB = true;
                bool compareSourceToMiniDB = true;
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

            if (createMiniDB)
            {
                CreateTablesMiniDB.RunScripts();//Creates the miniDB
            }

            if (createCatalog)
            {
                TestCatalogWriter.WriteCatalog();//Writes a catalog
            }

            StreamWriter Time = new StreamWriter(@"C:\Time\MiniDBPopulated.txt");
            Time.WriteLine("Start: " + DateTime.Now);

            if (populateMiniDB)
            {
                DataBaseGenerator.ReadCatalog(baseAddress).Wait();//Reads the catalog and populates miniDB
            }

            Time.WriteLine("End: " + DateTime.Now);
            Time.Close();

            if (compareSourceToMiniDB)
            {
                string connectionStringSource = ConfigurationManager.AppSettings["SourceDBConnectionString"];
                string connectionStringMiniDB = ConfigurationManager.AppSettings["MiniDBLocal"];
                DBComparer dbComparer = new DBComparer();
                dbComparer.ValidateDataIntegrity(connectionStringSource, connectionStringMiniDB);//Compare miniDB and source DB- check for data integrity
                Console.WriteLine(@"Please find the JSON report in C:\TEMP");
            }
        }
    }
}
