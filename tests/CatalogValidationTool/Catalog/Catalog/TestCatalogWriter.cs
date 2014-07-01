using NuGet.Services.Metadata.Catalog.GalleryIntegration;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Configuration;

namespace CatalogTestTool
{
    public class TestCatalogWriter
    {
        /*Writes the catalog using the given source */
        public static void WriteCatalog()
        {
            const int SqlChunkSize = 2000;
            //Initial Catalog points to the DataBase being used as Source
            string sqlConnectionString = ConfigurationManager.AppSettings["SourceDBConnectionString"]; ;

            const int CatalogBatchSize = 1000;
            const int CatalogMaxPageSize = 1000;

            Storage storage = new FileStorage
            {
                Path = @"c:\CatalogTest\test",
                Container = "test",
                BaseAddress = "http://localhost:8000"
            };

            //TODO: CONVERT THE STORAGE TO AZURE STORAGE INSTEAD OF ON DISC
            //************************AZURE STORAGE***************************
            //Storage storage = new AzureStorage
            //{
            //    AccountName = "nuget3",
            //    AccountKey = "",
            //    Container = "export",
            //    BaseAddress = "http://nuget3.blob.core.windows.net/"
            //};

            CatalogWriter writer = new CatalogWriter(storage, new CatalogContext(), CatalogMaxPageSize);

            GalleryExportBatcher batcher = new GalleryExportBatcher(CatalogBatchSize, writer);//cursor timestamp

            int lastHighestPackageKey = 0;

            while (true)
            {
                Tuple<int, int> range = GalleryExport.GetNextRange(sqlConnectionString, lastHighestPackageKey, SqlChunkSize);

                if (range.Item1 == 0 && range.Item2 == 0)
                {
                    break;
                }

                Console.WriteLine("{0} {1}", range.Item1, range.Item2);

                GalleryExport.FetchRange(sqlConnectionString, range, batcher);

                lastHighestPackageKey = range.Item2;
            }

            batcher.Complete();

            Console.WriteLine(batcher.Total);
        }
    }
}

 