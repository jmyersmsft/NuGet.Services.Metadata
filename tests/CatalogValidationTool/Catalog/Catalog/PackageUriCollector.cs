using JsonLD.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Collecting;
using System.IO;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Configuration;

namespace CatalogTestTool
{

    public class PackageUriCollector : Collector
    {

        public static string GetStringOrDefault(JObject dataObj, string property)
        {

            JToken propertyJToken;
            string propertyString;
            if (!dataObj.TryGetValue(property, out propertyJToken))
            {
                propertyString = string.Empty;
            }

            else propertyString = dataObj[property].ToString();

            return propertyString;
        }

        public static T GetObjectOrDefault<T>(JObject dataObj, string property)
        {

            JToken propertyJToken;
            T propertyObject;
            if (!dataObj.TryGetValue(property, out propertyJToken))
            {
                propertyObject = default(T);
            }

            else propertyObject = dataObj[property].ToObject<T>();

            return propertyObject;
        }

        public static bool PopulateDB()
        {
            StreamWriter writer = new StreamWriter(@"C:\TEMP\CatalogCollectorTime.txt");
            string uri = ConfigurationManager.AppSettings["CatalogAddress"];
            Uri index = new Uri(uri);
            CollectorCursor last = DateTime.MinValue;

            var collector = new PackageUriCollector(1000);
            writer.WriteLine("Start Dictionary: " + DateTime.Now);
            collector.Run(index, last).Wait();
            writer.WriteLine("End Dictionary: " + DateTime.Now);
            int DBEntrycount = 0;

            writer.WriteLine("Start DB: " + DateTime.Now);
            Parallel.ForEach(collector.DictionaryOfPackages, new ParallelOptions { MaxDegreeOfParallelism = 8}, p =>
            {
                string id = string.Empty;
                string version = string.Empty;
                int key = 0;
                HttpClient client = null;
                try
                {
                    client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(500);

                }

                catch
                {
                    Console.WriteLine("HttpClient failed");
                }

                try
                {
                    string sqlConnectionString = ConfigurationManager.AppSettings["MiniDBLocal"];
                    using (SqlConnection connection = new SqlConnection(sqlConnectionString))
                    {
                        connection.Open();
                        DateTime lastTime = DateTime.MinValue;
                        string dataJson = client.GetStringAsync(p.Value).Result;
                        JObject dataObj = JObject.Parse(dataJson);

                        //read the meta data required from each package


                        key = GetObjectOrDefault<Int32>(dataObj, "catalog:galleryKey");
                        id = GetStringOrDefault(dataObj, "id");
                        SqlDateTime created = GetObjectOrDefault<SqlDateTime>(dataObj, "created");
                        string description = GetStringOrDefault(dataObj, "description");
                        bool isLatest = GetObjectOrDefault<bool>(dataObj, "isLatest");
                        string tag = GetStringOrDefault(dataObj, "tag");
                        string tagFormatted = ParseTags(tag);
                        string summary = GetStringOrDefault(dataObj, "summary");
                        version = GetStringOrDefault(dataObj, "version");

                        string[] authorsArray;
                        List<string> authorsList;
                        string authors = GetStringOrDefault(dataObj, "authors");
                        authorsArray = authors.Split(',');
                        authorsList = new List<string>(authorsArray);

                        bool isLatestStable = GetObjectOrDefault<bool>(dataObj, "isLatestStable");
                        bool isPrerelease = GetObjectOrDefault<bool>(dataObj, "isPrerelease");
                        SqlDateTime published = GetObjectOrDefault<SqlDateTime>(dataObj, "published");
                        string projectUrl = GetStringOrDefault(dataObj, "projectUrl");
                        string licenseUrl = GetStringOrDefault(dataObj, "licenseUrl");
                        bool requiresLicenseAcceptance = GetObjectOrDefault<bool>(dataObj, "requiresLicenseAcceptance");
                        string minClientVersion = GetStringOrDefault(dataObj, "minClientVersion");
                        string title = GetStringOrDefault(dataObj, "title");
                        string language = GetStringOrDefault(dataObj, "language");
                        long downloadCount = GetObjectOrDefault<long>(dataObj, "downloadCount");

                        List<Tuple<string, string>> ListOfDependencies = new List<Tuple<string, string>>();
                        JToken dependencies;
                        if (dataObj.TryGetValue("dependencies", out dependencies))
                        {
                            foreach (JToken group in dependencies["group"])
                            {
                                string targetFrameworkOfDependencies = null;
                                try
                                {
                                    targetFrameworkOfDependencies = group["targetFramework"].ToString();
                                }

                                catch
                                {
                                    targetFrameworkOfDependencies = "";
                                }

                                foreach (JToken dependency in group["dependency"])
                                {
                                    string dependencyId = "";
                                    try
                                    {
                                        dependencyId = dependency["packageId"].ToString();

                                    }

                                    catch
                                    {
                                        dependencyId = "";
                                    }

                                    ListOfDependencies.Add(new Tuple<string, string>(targetFrameworkOfDependencies, dependencyId));
                                }
                            }
                        }

                        JToken targetFramework;
                        List<string> targetFrameworksList = new List<string>();

                        if (dataObj.TryGetValue("targetFramework", out targetFramework))
                        {
                            foreach (string tf in targetFramework)
                            {
                                targetFrameworksList.Add(tf);

                            }
                        }

                        else targetFrameworksList.Add("");

                        //Checks if a PackageRegistrations entry already exists
                        SqlCommand checkPackageRegistrations = new SqlCommand("SELECT count(*) FROM PackageRegistrations WHERE [Id] = @ID", connection);
                        checkPackageRegistrations.Parameters.AddWithValue("@ID", id);
                        string checkString = checkPackageRegistrations.ExecuteScalar().ToString();
                        int check = Convert.ToInt32(checkString);

                        if (check == 0)
                        {
                            //if the entry does not exist, Add it to the PackageRegistrations Table on the DB
                            SqlCommand commandPackageRegistrations = new SqlCommand("INSERT INTO PackageRegistrations (Id, DownloadCount)" +
                                "VALUES (@ID,0)", connection);
                            commandPackageRegistrations.Parameters.AddWithValue("@ID", id);
                            ExecuteNonQueryWithRetry(commandPackageRegistrations);

                        }

                        SqlCommand getIDENTITY = new SqlCommand("SELECT [Key] from PackageRegistrations WHERE [Id]=@ID", connection);
                        getIDENTITY.Parameters.AddWithValue("@ID", id);
                        string identity = getIDENTITY.ExecuteScalar().ToString();

                        //Populate Packages TABLE
                        SqlCommand commandPackages = new SqlCommand("INSERT INTO Packages (PackageRegistrationKey,GalleryKey,Created, Description, DownloadCount," +
                            "Hash, IsLatest, LastUpdated, LicenseUrl, Published, PackageFileSize,ProjectUrl,RequiresLicenseAcceptance, Summary, Tags, Title, Version," +
                            "IsLatestStable, Listed, IsPrerelease, Language, HideLicenseReport) VALUES (@IDENTITY,@GalleryKey, @created, @DSCRP, @downloadCount, 'null', 0 , @IsLatest , @lUrl ," +
                            " @published, 0, @pUrl, @requiresLicenseAcceptance,@summary,@tag,@title,@vers, @IsLatestStable, 0, @preRelease, @lang, 0);", connection);
                        commandPackages.Parameters.AddWithValue("@IDENTITY", identity);
                        commandPackages.Parameters.AddWithValue("@GalleryKey", key);
                        commandPackages.Parameters.AddWithValue("@DSCRP", description);
                        commandPackages.Parameters.AddWithValue("@tag", tagFormatted);
                        commandPackages.Parameters.AddWithValue("@title", title);
                        commandPackages.Parameters.AddWithValue("@summary", summary);
                        commandPackages.Parameters.AddWithValue("@vers", version);
                        commandPackages.Parameters.AddWithValue("@downloadCount", downloadCount);
                        commandPackages.Parameters.AddWithValue("@pUrl", projectUrl);
                        commandPackages.Parameters.AddWithValue("@lUrl", licenseUrl);
                        commandPackages.Parameters.AddWithValue("@lang", language);
                        //commandPackages.Parameters.AddWithValue("@rNotes", releaseNotes);
                        commandPackages.Parameters.AddWithValue("@created", created);
                        commandPackages.Parameters.AddWithValue("@IsLatest", isLatest);
                        commandPackages.Parameters.AddWithValue("@IsLatestStable", isLatestStable);
                        commandPackages.Parameters.AddWithValue("@published", published);
                        commandPackages.Parameters.AddWithValue("@preRelease", isPrerelease);
                        commandPackages.Parameters.AddWithValue("@requiresLicenseAcceptance", false);
                        ExecuteNonQueryWithRetry(commandPackages);


                        //Populate PackageAuthors TABLE
                        foreach (var author in authorsList)
                        {
                            SqlCommand commandPackageAuthors = new SqlCommand("INSERT INTO PackageAuthors (Name, PackageKey) " +
                                    "VALUES (@authors, @KEY)", connection);
                            commandPackageAuthors.Parameters.AddWithValue("@authors", author);
                            commandPackageAuthors.Parameters.AddWithValue("@KEY", key);
                            ExecuteNonQueryWithRetry(commandPackageAuthors);

                        }
                        //Populate PackageFrameworks TABLE
                        foreach (var targetframework in targetFrameworksList)
                        {
                            SqlCommand commandPackageFrameworks = new SqlCommand("INSERT INTO PackageFrameworks (TargetFramework, Package_Key) " +
                                "VALUES (@TF, @KEY)", connection);
                            commandPackageFrameworks.Parameters.AddWithValue("@TF", targetframework);
                            commandPackageFrameworks.Parameters.AddWithValue("@KEY", key);
                            ExecuteNonQueryWithRetry(commandPackageFrameworks);

                        }

                        //Populate PackageDependencies TABLE
                        foreach (var dependency in ListOfDependencies)
                        {
                            SqlCommand commandPackageDependencies = new SqlCommand("INSERT INTO PackageDependencies (PackageKey, Id, TargetFramework)" +
                            "VALUES (@KEY,@DependencyId,@TF)", connection);
                            commandPackageDependencies.Parameters.AddWithValue("@KEY", key);
                            commandPackageDependencies.Parameters.AddWithValue("@TF", dependency.Item1);
                            commandPackageDependencies.Parameters.AddWithValue("@DependencyId", dependency.Item2);
                            ExecuteNonQueryWithRetry(commandPackageDependencies);
                        }
                    }

                    DBEntrycount++;
                    Console.WriteLine("Count: {0}", DBEntrycount);
                }

                catch (Exception e)
                {
                    
                    throw new Exception(string.Format("{0} {1} {2}", key, id, version), e);
                }
            }


               );

            writer.WriteLine("End DB: " + DateTime.Now);
            writer.Close();

            return true;
        }




        int _batchSize;

        public PackageUriCollector(int batchSize)
        {
            _batchSize = batchSize;
            DictionaryOfPackages = new Dictionary<Tuple<string, string>, string>();
        }

        public int PageCount
        {
            private set;
            get;
        }

        public Dictionary<Tuple<string, string>, string> DictionaryOfPackages
        {
            private set;
            get;
        }

        protected override async Task<CollectorCursor> Fetch(CollectorHttpClient client, Uri index, CollectorCursor last)
        {
            CollectorCursor cursor = last;
            DateTime lastDateTime = (DateTime)last;

            IList<JObject> items = new List<JObject>();

            JObject root = await client.GetJObjectAsync(index);

            JToken context = null;
            root.TryGetValue("@context", out context);

            IEnumerable<JToken> rootItems = root["items"].OrderBy(item => item["commitTimestamp"].ToObject<DateTime>());

            foreach (JObject rootItem in rootItems)
            {
                DateTime pageTimeStamp = rootItem["commitTimestamp"].ToObject<DateTime>();

                if (pageTimeStamp > lastDateTime)
                {
                    Uri pageUri = rootItem["url"].ToObject<Uri>();
                    JObject page = await client.GetJObjectAsync(pageUri);

                    IEnumerable<JToken> pageItems = page["items"].OrderBy(item => item["commitTimestamp"].ToObject<DateTime>());

                    foreach (JObject pageItem in pageItems)
                    {
                        DateTime itemTimeStamp = pageItem["commitTimestamp"].ToObject<DateTime>();

                        if (itemTimeStamp > lastDateTime)
                        {
                            cursor = itemTimeStamp;

                            Uri itemUri = pageItem["url"].ToObject<Uri>();

                            items.Add(pageItem);

                            if (items.Count == _batchSize)
                            {
                                await ProcessBatch(client, items, (JObject)context);
                                PageCount++;
                                items.Clear();
                            }
                        }
                    }
                }
            }

            if (items.Count > 0)
            {
                await ProcessBatch(client, items, (JObject)context);
                PageCount++;
            }

            return cursor;
        }

        public static void ExecuteNonQueryWithRetry(SqlCommand cmd)
        {
            const int RetryCount = 10;

            cmd.CommandTimeout = 5 * 60;

            int tries = 0;
            do
            {
                try
                {
                    cmd.ExecuteNonQuery();
                    return;
                }
                catch (Exception ex)
                {
                    if (tries < RetryCount)
                    {
                        Console.WriteLine("Error: " + ex.ToString());
                        Console.WriteLine("Retrying");
                        tries++;
                    }
                    else
                    {
                        throw;
                    }
                }
            } while (tries < RetryCount);
        }

        public static string ParseTags(string tags)
        {
            //Parse tags from catalog to match the format of the tags in the source DB
            if (tags == null)
            {
                return null;
            }
            return tags.Replace(',', ' ').Replace(';', ' ').Replace('\t', ' ').Replace('[', ' ').Replace(']', ' ').Replace('"', ' ').TrimEnd().TrimStart();//.Replace(" ", "");
        }


        protected Task ProcessBatch(CollectorHttpClient client, IList<JObject> items, JObject context)
        {

            try
            {
                foreach (var item in items)
                {
                    string type = item.Value<string>("@type");
                    string url = item.Value<string>("url");
                    var id = item.Value<string>("nuget:id");
                    var version = item.Value<string>("nuget:version");
                    var key = Int32.Parse(item.Value<string>("galleryKey"));
                    Tuple<string, string> dictKey = new Tuple<string, string>(id, version);
                    if (String.Equals(type, "nuget:Package", StringComparison.Ordinal))
                    {
                        DictionaryOfPackages.Add(dictKey, url);
                    }

                    else if (String.Equals(type, "nuget:PackageDeletion", StringComparison.Ordinal))
                    {
                        if (DictionaryOfPackages.ContainsKey(dictKey))
                        {
                            DictionaryOfPackages.Remove(dictKey);
                        }

                    }

                }
            }

            catch
            {
                throw;
            }

            using (StreamWriter packages = new StreamWriter(@"C:\TEMP\PackagesFromCollector.txt"))
            {
                foreach (KeyValuePair<Tuple<string, string>, string> package in DictionaryOfPackages)
                {
                    packages.WriteLine(package.Key + " " + package.Value);
                }
            }

            Console.WriteLine("Here");
            return Task.FromResult(0);
        }
    }
}