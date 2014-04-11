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
        public IList<OwnerAssertion> Owners { get; set; }
    }
    public class PackageAssertion : PackageMinAssertion
    {
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
        public string PackageId { get; set; }
        public string Version { get; set; }

        public OwnerAssertion GetOwnerAssertionAlone()
        {
            var ownerAssertion = new OwnerAssertion();
            ownerAssertion.UserName = this.UserName;
            ownerAssertion.Exists = this.Exists;
            return ownerAssertion;
        }
    }

    public static class EventStreamQueries
    {
        public const string GetEventsQuery = @"DECLARE		@PackageAssertions TABLE
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
            var jArray = new JArray();
            try
            {
                using (var connection = await sql.ConnectTo())
                {
                    Console.WriteLine("Connected to database in {0}/{1} obtained: {2}", connection.DataSource, connection.Database, connection.ClientConnectionId);
                    Console.WriteLine("Querying multiple queries...");
                    var results = connection.QueryMultiple(EventStreamQueries.GetEventsQuery);
                    Console.WriteLine("Completed multiple queries.");

                    Console.WriteLine("Extracting packageassertions and owner assertions...");
                    var packageAssertions = results.Read<PackageAssertion>();
                    var packageOwnerAssertions = results.Read<PackageOwnerAssertion>();

                    var packagesAndOwners = new Dictionary<Tuple<string, string>, PackageMinAssertion>();
                    foreach(var packageAssertion in packageAssertions)
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
                        }
                        if (packageAssertion.Owners == null)
                        {
                            packageAssertion.Owners = new List<OwnerAssertion>();
                        }

                        packageAssertion.Owners.Add(packageOwnerAssertion.GetOwnerAssertionAlone());
                    }

                    foreach (var value in packagesAndOwners.Values)
                    {
                        var packageAssertionJObject = (JObject)JToken.FromObject(value);
                        jArray.Add(packageAssertionJObject);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            var timeStamp = DateTime.UtcNow;
            var blobName = String.Format(EventFileNameFormat, EventsPrefix, timeStamp.ToString(DateTimeFormat));

            var json = new JObject();
            json.Add(EventTimeStamp, timeStamp);
            json.Add(EventPrevious, EventNull);
            json.Add(EventNext, EventNull);
            json.Add(EventAssertions, jArray);
            await DumpJSON(json, blobName);

            return json;
        }

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
    }
}
