using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Configuration;

namespace CatalogTestTool
{
    class DataBaseGenerator
    {
        internal static async Task ReadCatalog(string baseAddress)
        {
            DateTime lastReadTime = TestCatalogWriter.lastTime;
            //DateTime lastReadTime = DateTime.Parse("5/28/2014 9:04:10 PM");//TODO: remove hardcoded lastReadTime

            Uri address = new Uri(string.Format(@"{0}index.json", baseAddress));
            HttpClient client = new HttpClient();

            string sqlConnectionString = ConfigurationManager.AppSettings["MiniDBLocal"];
            using (SqlConnection connection = new SqlConnection(sqlConnectionString))
            {
                try
                {
                    //connect to the miniDB 
                    connection.Open();
                }

                catch (SqlException sqlEx)
                {
                    Console.WriteLine(sqlEx.Message);
                }

                catch (System.Exception e)
                {
                    Console.WriteLine(e.Message);
                }

                //parse the JSON and traverse down to the individual items (packages)
                string indexJson = await client.GetStringAsync(address);
                JObject indexObj = JObject.Parse(indexJson);
                int count = 0;
                try
                {

                    foreach (JToken indexItem in indexObj["items"])
                    {
                        DateTime indexItemTimeStamp = indexItem["commitTimestamp"].ToObject<DateTime>();

                        if (indexItemTimeStamp > lastReadTime)
                        {
                            string pageJson = await client.GetStringAsync(indexItem["url"].ToObject<Uri>());
                            JObject pageObj = JObject.Parse(pageJson);

                            foreach (JToken pageItem in pageObj["items"])
                            {
                                DateTime pageItemTimeStamp = pageItem["commitTimestamp"].ToObject<DateTime>();

                                if (pageItemTimeStamp > lastReadTime)
                                {
                                    string dataJson = await client.GetStringAsync(pageItem["url"].ToObject<Uri>());
                                    JObject dataObj = JObject.Parse(dataJson);
                                    count++;
                                    //read the meta data required from each package
                                    int key = dataObj["catalog:galleryKey"].ToObject<Int32>();
                                    string id = dataObj["id"].ToString();
                                    SqlDateTime created = dataObj["created"].ToObject<SqlDateTime>();

                                    string description = null;
                                    try
                                    {
                                        description = dataObj["description"].ToString();
                                    }

                                    catch
                                    {
                                        description = "";
                                    }

                                    bool isLatest = dataObj["isLatest"].ToObject<bool>();
                                    string tag = null;
                                    try
                                    {
                                        tag = dataObj["tag"].ToString();
                                    }

                                    catch
                                    {
                                        tag = "";
                                    }

                                    string tagFormatted = ParseTags(tag);
                                    string summary = null;
                                    try
                                    {
                                        summary = dataObj["summary"].ToString();
                                    }

                                    catch
                                    {
                                        summary = "";
                                    }

                                    string version = dataObj["version"].ToString();

                                    JToken authors;
                                    string[] authorsArray;
                                    List<string> authorsList;
                                    if (dataObj.TryGetValue("authors", out authors))
                                    {
                                        authorsArray = authors.ToString().Split(',');
                                        authorsList = new List<string>(authorsArray);

                                    }

                                    else
                                    {
                                        authorsList = new List<string>();
                                        authorsList.Add("");
                                    }

                                    bool isLatestStable = dataObj["isLatestStable"].ToObject<bool>();
                                    bool isPrerelease = dataObj["isPrerelease"].ToObject<bool>();
                                    SqlDateTime published = dataObj["published"].ToObject<SqlDateTime>();
                                    string projectUrl = "";
                                    try
                                    {
                                        projectUrl = dataObj["projectUrl"].ToString();
                                    }
                                    catch
                                    {
                                        projectUrl = "";
                                    }

                                    string licenseUrl = "";
                                    try
                                    {
                                        licenseUrl = dataObj["licenseUrl"].ToString();
                                    }
                                    catch
                                    {
                                        licenseUrl = "";
                                    }

                                    //bool requiresLicenseAcceptance = dataObj["requiresLicenseAcceptance"].ToObject<bool>();
                                    string title = "";
                                    try
                                    {
                                        title = dataObj["title"].ToString();
                                    }
                                    catch
                                    {
                                        title = "";
                                    }



                                    //string releaseNotes = dataObj["releaseNotes"].ToString();
                                    string language = "";
                                    try
                                    {
                                        language = dataObj["language"].ToString();
                                    }

                                    catch
                                    {
                                        language = "";
                                    }

                                    string downloadCountString = ""; long downloadCount = 0;
                                    try
                                    {
                                        downloadCountString = dataObj["downloadCount"].ToString();
                                        downloadCount = Convert.ToInt64(downloadCountString);
                                    }

                                    catch
                                    {
                                        downloadCountString = "";
                                    }


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

                            }
                        }
                    }

                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.ToString());
                }
            }


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

        //public static string ExecuteScalarWithRetry(SqlCommand cmd)
        //{
        //    const int RetryCount = 10;

        //    cmd.CommandTimeout = 5 * 60;
        //    string result;
        //    int tries = 0;
        //    do
        //    {
        //        try
        //        {
        //            result=cmd.ExecuteScalar().ToString();
        //            return result;
        //        }
        //        catch (Exception ex)
        //        {
        //            if (tries < RetryCount)
        //            {
        //                Console.WriteLine("Error: " + ex.ToString());
        //                Console.WriteLine("Retrying");
        //                tries++;
        //            }
        //            else
        //            {
        //                throw;
        //            }
        //        }
        //    } while (tries < RetryCount);
        //}

        public static string ParseTags(string tags)
        {
            //Parse tags from catalog to match the format of the tags in the source DB
            if (tags == null)
            {
                return null;
            }
            return tags.Replace(',', ' ').Replace(';', ' ').Replace('\t', ' ').Replace('[', ' ').Replace(']', ' ').Replace('"', ' ').TrimEnd().TrimStart();//.Replace(" ", "");
        }

        

      
    }
}
