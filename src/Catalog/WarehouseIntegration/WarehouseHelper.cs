using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.WarehouseIntegration
{
    public class WarehouseHelper
    {
        static Newtonsoft.Json.Linq.JArray GetNextBatch(string connectionString, ref int lastKey, out DateTime minDownloadTimeStamp, out DateTime maxDownloadTimeStamp)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string cmdText = @"
                    SELECT TOP(1000) 
	                    PackageStatistics.[Key],
	                    PackageStatistics.[TimeStamp],
	                    PackageRegistrations.Id,
	                    Packages.[NormalizedVersion],
	                    ISNULL(PackageStatistics.UserAgent, ''),
	                    ISNULL(PackageStatistics.Operation, ''), 
	                    ISNULL(PackageStatistics.DependentPackage, ''),
	                    ISNULL(PackageStatistics.ProjectGuids, ''),
	                    ISNULL(Packages.Title, ''),
	                    ISNULL(Packages.[Description], ''),
	                    ISNULL(Packages.IconUrl, '')
                    FROM PackageStatistics
                    INNER JOIN Packages ON PackageStatistics.PackageKey = Packages.[Key]
                    INNER JOIN PackageRegistrations ON Packages.PackageRegistrationKey = PackageRegistrations.[Key]
                    WHERE PackageStatistics.[Key] > @key
                    ORDER BY PackageStatistics.[Key]";

                SqlCommand command = new SqlCommand(cmdText, connection);
                command.Parameters.AddWithValue("key", lastKey);

                SqlDataReader reader = command.ExecuteReader();

                int count = 0;

                minDownloadTimeStamp = DateTime.MaxValue;
                maxDownloadTimeStamp = DateTime.MinValue;

                JArray batch = new JArray();

                while (reader.Read())
                {
                    count++;

                    int key = reader.GetInt32(0);
                    if (key > lastKey)
                    {
                        lastKey = key;
                    }

                    DateTime timeStamp = reader.GetDateTime(1);
                    if (timeStamp < minDownloadTimeStamp)
                    {
                        minDownloadTimeStamp = timeStamp;
                    }

                    if (timeStamp > maxDownloadTimeStamp)
                    {
                        maxDownloadTimeStamp = timeStamp;
                    }

                    JArray row = new JArray();

                    //row.Add(reader.GetInt32(0));
                    row.Add(reader.GetDateTime(1).ToString("O"));
                    row.Add(reader.GetString(2));
                    row.Add(reader.GetString(3));
                    row.Add(reader.GetString(4));
                    row.Add(reader.GetString(5));
                    row.Add(reader.GetString(6));
                    row.Add(reader.GetString(7));
                    row.Add(reader.GetString(8));
                    row.Add(reader.GetString(9));
                    row.Add(reader.GetString(10));

                    batch.Add(row);
                }

                Trace.TraceInformation("{0} {1}", lastKey, count);

                if (count == 0)
                {
                    minDownloadTimeStamp = DateTime.MinValue;
                    return null;
                }

                return batch;
            }
        }

        public static async Task CreateStatisticsCatalogAsync(Storage storage, string connectionString)
        {
            const int BatchSize = 100;
            int i = 0;

            const string dateTimeFormat = "yyyy.MM.dd.HH.mm.ss";
            const string pageNumberFormat = "{0}_TO_{1}";

            using (CatalogWriter writer = new CatalogWriter(storage, new CatalogContext(), 500))
            {
                int lastKey = 0;
                int iterations = 0;

                while (true)
                {
                    iterations++;

                    DateTime minDownloadTimeStamp;
                    DateTime maxDownloadTimeStamp;

                    JArray batch = GetNextBatch(connectionString, ref lastKey, out minDownloadTimeStamp, out maxDownloadTimeStamp);

                    if (batch == null)
                    {
                        break;
                    }

                    string pageNumber = String.Format(pageNumberFormat, minDownloadTimeStamp.ToString(dateTimeFormat), maxDownloadTimeStamp.ToString(dateTimeFormat));
                    writer.Add(new StatisticsCatalogItem(batch, pageNumber, minDownloadTimeStamp, maxDownloadTimeStamp));

                    if (++i % BatchSize == 0)
                    {
                        await writer.Commit();
                    }
                }

                await writer.Commit();
            }
        }

        public static async Task PostToMetricsService(string connectionString)
        {
            int lastKey = 0;
            DateTime minDownloadTimeStamp;
            DateTime maxDownloadTimeStamp;

            string metricsService = "http://localhost:12345/DownloadEvent";
            using (var httpClient = new HttpClient()
                                    {
                                        BaseAddress = new Uri(metricsService)
                                    })
            {
                for(int i = 0; i < 1; i++)
                {
                    JArray batch = GetNextBatch(connectionString, ref lastKey, out minDownloadTimeStamp, out maxDownloadTimeStamp);
                    JArray formattedBatch = new JArray();
                    int j = 0;
                    foreach(var item in batch)
                    {
                        j++;
                        JObject jObject = GetJObject(item as JArray);
                        var response = await httpClient.PostAsync(metricsService, new StringContent(jObject.ToString(), Encoding.Default, "application/json"));
                        Trace.WriteLine("Posting for package id '" + jObject[IdKey] + "' and version '" + jObject[VersionKey] + "'...");
                        if(j%4 == 0)
                        {
                            Trace.WriteLine("Sleeping...");
                            Thread.Sleep(2000);
                        }

                        if(j >= 20)
                        {
                            break;
                        }
                        //formattedBatch.Add(jObject);
                    }

                    //var response = await httpClient.PostAsync(metricsService, new StringContent(formattedBatch.ToString(), Encoding.Default, "application/json"));
                    //Trace.WriteLine("Completed batch number" + i + 1 + ". Sleeping for a second...");
                    //Thread.Sleep(1000);
                }
            }
        }

        const string IdKey = "id";
        const string VersionKey = "version";
        const string IPAddressKey = "ipAddress";
        const string UserAgentKey = "userAgent";
        const string OperationKey = "operation";
        const string DependentPackageKey = "dependentPackage";
        const string ProjectGuidsKey = "projectGuids";
        private static JObject GetJObject(JArray row)
        {
            var jObject = new JObject();
            jObject.Add(IdKey, row[6].ToString());
            jObject.Add(VersionKey, row[7].ToString());
            jObject.Add(UserAgentKey, row[2].ToString());
            jObject.Add(OperationKey, row[3].ToString());
            jObject.Add(DependentPackageKey, row[4].ToString());
            jObject.Add(ProjectGuidsKey, row[5].ToString());

            return jObject;
        }
    }
}
