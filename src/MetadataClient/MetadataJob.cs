using System;
using System.Text;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace MetadataClient
{
    public class PackageMinAssertion
    {
        public string PackageId { get; set; }
        public string Version { get; set; }
        public bool Exists { get; set; }
        public IList<OwnerAssertion> Owners { get; set; }
    }
    public class PackageAssertion : PackageMinAssertion
    {
        [JsonIgnore]
        public int Key { get; set; }

        public string Nupkg { get; set; }
        public bool Listed { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Published { get; set; }
    }

    public class OwnerAssertion
    {
        public string UserName { get; set; }
        public bool Exists { get; set; }
    }

    public class PackageOwnerAssertion : OwnerAssertion
    {
        [JsonIgnore]
        public int Key { get; set; }
        [JsonIgnore]
        public string PackageId { get; set; }
        [JsonIgnore]
        public string Version { get; set; }
    }

    public class IndexJson
    {
        public DateTime LastUpdated { get; set; }
        public string OldestEventStream { get; set; }
        public string NewestEventStream { get; set; }
    }

    public static class AssertionQueries
    {
        public const string GetAssertionsQuery = @"DECLARE		@PackageAssertions TABLE
(
			[Key] int
		,	PackageId nvarchar(128)
		,	[Version] nvarchar(64)
)

DECLARE		@PackageOwnerAssertions TABLE
(
			[Key] int
		,	Username nvarchar(64)
		,	PackageId nvarchar(128)
		,	[Version] nvarchar(64)
)

DECLARE		@ProcessingDateTime datetime = GETUTCDATE()

BEGIN TRAN

UPDATE		LogPackages
SET			ProcessAttempts = ProcessAttempts + 1
		,	FirstProcessingDateTime = ISNULL(FirstProcessingDateTime, @ProcessingDateTime)
		,	LastProcessingDateTime = @ProcessingDateTime
OUTPUT		inserted.[Key]
		,	inserted.PackageId
		,	inserted.[Version]
INTO		@PackageAssertions
WHERE		ProcessedDateTime IS NULL

UPDATE		LogPackageOwners
SET			ProcessAttempts = ProcessAttempts + 1
		,	FirstProcessingDateTime = ISNULL(FirstProcessingDateTime, @ProcessingDateTime)
		,	LastProcessingDateTime = @ProcessingDateTime
OUTPUT		inserted.[Key]
		,	inserted.Username
		,	inserted.PackageId
		,	inserted.Version
INTO		@PackageOwnerAssertions
WHERE		ProcessedDateTime IS NULL

COMMIT TRAN

SELECT		LogPackages.*
FROM		(
			SELECT		MaxKey = MAX([Key])
					,	PackageId
					,	[Version]
			FROM		@PackageAssertions
			GROUP BY	PackageId
					,	[Version]
			) PackageAssertions
INNER JOIN	LogPackages WITH (NOLOCK)
		ON	LogPackages.[Key] = PackageAssertions.MaxKey

SELECT		LogPackageOwners.*
FROM		(
			SELECT		MaxKey = MAX([Key])
					,	Username
					,	PackageId
					,	[Version]
			FROM		@PackageOwnerAssertions
			GROUP BY	Username
					,	PackageId
					,	[Version]
			) PackageOwnerAssertions
INNER JOIN	LogPackageOwners WITH (NOLOCK)
		ON	LogPackageOwners.[Key] = PackageOwnerAssertions.MaxKey";

        public const string MarkAssertionsQuery = @"DECLARE		@ProcessedDateTime datetime = GETUTCDATE()

UPDATE		LogPackages
SET			ProcessedDateTime = @ProcessedDateTime
WHERE		[Key] IN @packageAssertionKeys

UPDATE		LogPackageOwners
SET			ProcessedDateTime = @ProcessedDateTime
WHERE		[Key] IN @packageOwnerAssertionKeys";
    }

    public static class MetadataJob
    {
        // Formatting constants
        private const string EventsPrefix = "packageassertions/";
        private const string DateTimeFormat = "yyyy/MM/dd/HH-mm-ss-fffZ";
        private const string EventFileNameFormat = "{0}{1}.json";

        // File constants
        private const string IndexJson = "index.json";

        // Property Name constants
        private const string EventTimeStamp = "timestamp";
        private const string EventPrevious = "previous";
        private const string EventNext = "next";
        private const string EventNull = "null";
        private const string EventAssertions = "assertions";
        private const string PackageId = "PackageId";
        private const string PackageVersion = "Version";
        private const string PackageNupkg = "nupkg";
        private const string PackageOwners = "owners";

        public static async Task Start(CloudStorageAccount blobAccount, CloudBlobContainer container, SqlConnectionStringBuilder sql, bool dumpToCloud)
        {
            Console.WriteLine("Started polling...");
            Console.WriteLine("Looking for changes in {0}/{1} ", sql.DataSource, sql.InitialCatalog);

            if (await container.CreateIfNotExistsAsync())
            {
                Console.WriteLine("Container created");
            }

            Container = container;
            DumpToCloud = dumpToCloud;

            // The blobAccount and container can potentially be used to put the trigger information
            // on package blobs or otherwise. Not Important now

            while (true)
            {
                try
                {
                    await DetectChanges(sql);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                Console.WriteLine('.');
                Thread.Sleep(3000);
            }
        }
        public static CloudBlobContainer Container
        {
            private get;
            set;
        }

        public static bool DumpToCloud
        {
            get;
            private set;
        }

        public static async Task<JObject> DetectChanges(SqlConnectionStringBuilder sql)
        {
            JObject json = null;

            try
            {
                using (var connection = await sql.ConnectTo())
                {
                    Console.WriteLine("Connected to database in {0}/{1} obtained: {2}", connection.DataSource, connection.Database, connection.ClientConnectionId);
                    Console.WriteLine("Querying multiple queries...");
                    var results = connection.QueryMultiple(AssertionQueries.GetAssertionsQuery);
                    Console.WriteLine("Completed multiple queries.");

                    Console.WriteLine("Extracting packageassertions and owner assertions...");
                    var packageAssertions = results.Read<PackageAssertion>();
                    var packageOwnerAssertions = results.Read<PackageOwnerAssertion>();

                    // Extract the assertions as JArray
                    var jArrayAssertions = GetJArrayAssertions(packageAssertions, packageOwnerAssertions);

                    if (jArrayAssertions.Count > 0)
                    {
                        var timeStamp = DateTime.UtcNow;
                        var indexJSONBlob = GetIndexJSON();
                        // Get Final JObject with timeStamp, previous, next links etc
                        json = GetJObject(jArrayAssertions, timeStamp, indexJSONBlob);

                        var blobName = GetBlobName(timeStamp);

                        // Write the blob
                        await DumpJSON(json, blobName);

                        // Update indexJSON blob and previous latest Blob
                        await UpdateIndex(indexJSONBlob, json);

                        // Mark assertions as processed
                        await MarkAssertionsAsProcessed(connection, packageAssertions, packageOwnerAssertions);
                    }
                    else
                    {
                        Console.WriteLine("No Assertions to make");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return json;
        }

        /// <summary>
        /// Gets the assertions as JArray from the packageAssertions and packageOwnerAssertions queried from the database
        /// This can be tested separately to verify that the right jArray of assertions are created using mocked assertions
        /// </summary>
        public static JArray GetJArrayAssertions(IEnumerable<PackageAssertion> packageAssertions, IEnumerable<PackageOwnerAssertion> packageOwnerAssertions)
        {
            var packagesAndOwners = new Dictionary<Tuple<string, string>, PackageMinAssertion>();
            foreach (var packageAssertion in packageAssertions)
            {
                packagesAndOwners.Add(new Tuple<string, string>(packageAssertion.PackageId, packageAssertion.Version), packageAssertion);
            }

            foreach (var packageOwnerAssertion in packageOwnerAssertions)
            {
                var key = new Tuple<string, string>(packageOwnerAssertion.PackageId, packageOwnerAssertion.Version);
                PackageMinAssertion packageAssertion = null;
                if (!packagesAndOwners.TryGetValue(key, out packageAssertion))
                {
                    packageAssertion = packagesAndOwners[key] = new PackageMinAssertion();
                    packageAssertion.PackageId = packageOwnerAssertion.PackageId;
                    packageAssertion.Version = packageOwnerAssertion.Version;
                    packageAssertion.Exists = true;
                }
                if (packageAssertion.Owners == null)
                {
                    packageAssertion.Owners = new List<OwnerAssertion>();
                }

                packageAssertion.Owners.Add(packageOwnerAssertion);
            }

            var jArray = new JArray();
            foreach (var value in packagesAndOwners.Values)
            {
                var packageAssertionJObject = (JObject)JToken.FromObject(value);
                jArray.Add(packageAssertionJObject);
            }

            return jArray;
        }

        private static CloudBlockBlob GetIndexJSON()
        {
            return null;
        }

        private static JObject GetJSON(CloudBlockBlob blob)
        {
            throw new NotImplementedException();
        }

        private static JObject GetJObject(JArray jArrayAssertions, DateTime timeStamp, CloudBlockBlob indexJSONBlob)
        {
            JObject indexJSON = null;
            if (indexJSONBlob != null)
            {
                indexJSON = GetJSON(indexJSONBlob);
            }

            JObject jObject = GetJObject(jArrayAssertions, timeStamp, indexJSON);

            return jObject;
        }

        /// <summary>
        /// Gets the final JObject given the assertions as jArray, timeStamp and indexJSON
        /// This can be tested separately to verify that the index is used and updated correctly using a mocked indexJSON JObject
        /// </summary>
        public static JObject GetJObject(JArray jArrayAssertions, DateTime timeStamp, JObject indexJSON)
        {
            var json = new JObject();
            json.Add(EventTimeStamp, timeStamp);
            if (indexJSON == null)
            {
                json.Add(EventPrevious, EventNull);
            }
            else
            {
                throw new NotImplementedException();
            }
            json.Add(EventNext, EventNull);
            json.Add(EventAssertions, jArrayAssertions);
            return json;
        }

        public static string GetBlobName(DateTime timeStamp)
        {
            return String.Format(EventFileNameFormat, EventsPrefix, timeStamp.ToString(DateTimeFormat));
        }

        /// <summary>
        /// This function simply dumps the json onto console and to the blob if applicable
        /// </summary>
        private static async Task DumpJSON(JObject json, string blobName)
        {
            if(json == null)
            {
                throw new ArgumentNullException("json");
            }

            Console.WriteLine("BlobName: {0}\n", blobName);

            var jsonString = json;
            if (DumpToCloud)
            {
                Console.WriteLine("Dumping to {0}", blobName);
                var blob = Container.GetBlockBlobReference(blobName);
                using (var stream = new MemoryStream(Encoding.Default.GetBytes(json.ToString()), false))
                {
                    await blob.UploadFromStreamAsync(stream);
                }
            }
            else
            {
                Console.WriteLine("Not Dumping to cloud...\n");
            }
            Console.WriteLine(jsonString);
        }

        private static async Task UpdateIndex(CloudBlockBlob indexJSONBlob, JObject latestEventStream)
        {
            if (indexJSONBlob != null)
            {
                // Acquire Leases
                // Update indexJSON blob and previous latest Blob
                // Release Leases
            }
        }

        private static async Task MarkAssertionsAsProcessed(SqlConnection connection, IEnumerable<PackageAssertion> packageAssertions,
            IEnumerable<PackageOwnerAssertion> packageOwnerAssertions)
        {
            var allPackageAssertionKeys = (from packageAssertion in packageAssertions
                                       select packageAssertion.Key).ToList();

            var allPackageOwnerAssertionKeys = (from packageOwnerAssertion in packageOwnerAssertions
                                            select packageOwnerAssertion.Key).ToList();

            // NOTE THAT the number of keys in the 'IN' clause of a SQL query is restricted to 2100
            // Let us keep it simple and restrict the number to 1000. Since there are 2 IN queries,
            // 1 for package assertions and 1 for package owner assertions, set the limit as 500
            var maxINClauseKeysCount = 500;
            var index = 0;

            var packageAssertionsCount = allPackageAssertionKeys.Count;
            var ownerAssertionsCount = allPackageOwnerAssertionKeys.Count;
            while (index < packageAssertionsCount || index < ownerAssertionsCount)
            {
                var packageAssertionKeys = index < packageAssertionsCount ?
                allPackageAssertionKeys.GetRange(index, Math.Min(maxINClauseKeysCount, packageAssertionsCount - index)) : new List<int>();
                var packageOwnerAssertionKeys = index < ownerAssertionsCount ?
                allPackageOwnerAssertionKeys.GetRange(index, Math.Min(maxINClauseKeysCount, ownerAssertionsCount - index)) : new List<int>();

                await connection.QueryAsync<int>(AssertionQueries.MarkAssertionsQuery,
                    new { packageAssertionKeys = packageAssertionKeys, packageOwnerAssertionKeys = packageOwnerAssertionKeys });

                index += maxINClauseKeysCount;
            }
        }
    }
}
