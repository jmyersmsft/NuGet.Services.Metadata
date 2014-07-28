using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Linq;
using System.Configuration;
using System.IO;

namespace CatalogTestTool
{
    public class DBComparer : IComparer
    {
        /* Invoked by DataBaseGenerator after creating the miniDB
         Input: connection strings to the source DB and mini DB
         Calls helper function, Compare() to perform comparisons between the the two DBs*/
        public int ValidateDataIntegrity(string connectionStringSource, string connectionStringMiniDB, StreamWriter totalTimeForRun)
        {
            /* In the following dictionaries, the Key consists of Package Key.
            The Value is a Tuple of the other metadata about each package. For class deinifitons, see PackageData.cs*/

            string SQLQuerySource = @"
               SELECT PackageRegistrations.[Id],PackageRegistrations.[DownloadCount],
                    Packages.[Key],Packages.Description,Packages.IconUrl,Packages.LicenseUrl,Packages.ProjectUrl,
                    Packages.Summary, Packages.Tags, Packages.Title, Packages.Version, Packages.ReleaseNotes, Packages.Language,
                    Packages.IsLatest,Packages.IsLatestStable,Packages.IsPrerelease,Packages.RequiresLicenseAcceptance,
                    Packages.Created,Packages.Published,
                    PackageAuthors.PackageKey,PackageAuthors.Name,
                    PackageFrameworks.[Package_Key] as packageKey,PackageFrameworks.TargetFramework,
                    PackageDependencies.PackageKey,PackageDependencies.[Id] as dependencyId,PackageDependencies.TargetFramework as dependencyFramework
                                   
                FROM Packages 

                INNER JOIN PackageRegistrations ON PackageRegistrations.[Key] = Packages.[PackageRegistrationKey]
                LEFT OUTER JOIN PackageDependencies ON Packages.[Key] = PackageDependencies.[PackageKey] 
				LEFT OUTER JOIN PackageAuthors ON Packages.[Key]=PackageAuthors.[PackageKey]            
				LEFT OUTER JOIN PackageFrameworks ON Packages.[Key] = PackageFrameworks.[Package_Key]
                ";

            string SQLQueryMiniDB = @"
               SELECT PackageRegistrations.[Id],PackageRegistrations.[DownloadCount],
                    Packages.[GalleryKey],Packages.Description,Packages.IconUrl,Packages.LicenseUrl,Packages.ProjectUrl,
                    Packages.Summary, Packages.Tags, Packages.Title, Packages.Version, Packages.ReleaseNotes, Packages.Language,
                    Packages.IsLatest,Packages.IsLatestStable,Packages.IsPrerelease,Packages.RequiresLicenseAcceptance,
                    Packages.Created,Packages.Published,
                    PackageAuthors.PackageKey,PackageAuthors.Name,
                    PackageFrameworks.[Package_Key] as packageKey,PackageFrameworks.TargetFramework,
                    PackageDependencies.PackageKey,PackageDependencies.[Id] as dependencyId,PackageDependencies.TargetFramework as dependencyFramework
                                   
                FROM Packages 

                INNER JOIN PackageRegistrations ON PackageRegistrations.[Key] = Packages.[PackageRegistrationKey]
                LEFT OUTER JOIN PackageDependencies ON Packages.[GalleryKey] = PackageDependencies.[PackageKey] 
				LEFT OUTER JOIN PackageAuthors ON Packages.[GalleryKey]=PackageAuthors.[PackageKey]            
				LEFT OUTER JOIN PackageFrameworks ON Packages.[GalleryKey] = PackageFrameworks.[Package_Key]
                ";
            Dictionary<int, Packages> sourceDictionary = GetPackageDictionary(connectionStringSource, SQLQuerySource, "Key");
            Dictionary<int, Packages> miniDBDictionary = GetPackageDictionary(connectionStringMiniDB, SQLQueryMiniDB, "GalleryKey");
            int packageCount = Compare(sourceDictionary, miniDBDictionary, totalTimeForRun);
            return packageCount;
        }


        public int Compare(Dictionary<int, Packages> source, Dictionary<int, Packages> miniDB, StreamWriter totalTimeForRun)
        {
            /* Input: Dictionaries populated with data from source DB and mini DB
             Each package is checked for the integrity of metadata*/
            AzureLogger azurelog = new AzureLogger();//Report logger initialized
            int packageCount = 0;
            using (StreamWriter writer = new StreamWriter(ConfigurationManager.AppSettings["JsonReport"]))//Where the report is logged
            {
                
                writer.WriteLine("Time started: " + DateTime.Now);
                foreach (KeyValuePair<int, Packages> entry in miniDB)
                {
                    List<string> errorMessages = new List<string>();
                    packageCount++;                   
                    Packages valueSource;
                    if (source.TryGetValue(entry.Key, out valueSource))//find corresponding key in the source DB
                    {
                        string messageRegistrations = "";
                        string messagePackages = "";
                        string messageFrameworks = "";
                        string messageDependencies = "";
                        string messageAuthors = "";

                        bool registration = Equals(valueSource.registration, entry.Value.registration, out messageRegistrations);
                        bool packages = Equals(valueSource, entry.Value, out messagePackages);
                        bool frameworks = Equals(valueSource.frameworks, entry.Value.frameworks, out messageFrameworks);
                        bool dependencies = Equals(valueSource.dependencies, entry.Value.dependencies, out messageDependencies);
                        bool authors = Equals(valueSource.authors, entry.Value.authors, out messageAuthors);

                        if ((!registration) && (!packages) && (!frameworks) && (!dependencies) && (!authors))
                        {
                            errorMessages.Add("No errors");
                        }

                        else
                        {
                            if (registration) errorMessages.Add(messageRegistrations);
                            if (packages) errorMessages.Add(messagePackages);
                            if (frameworks) errorMessages.Add(messageFrameworks);
                            if (dependencies) errorMessages.Add(messageDependencies);
                            if (authors) errorMessages.Add(messageAuthors);
                            azurelog.ReportDictionary.Add(valueSource.registration.id + " " + valueSource.version, errorMessages);
                            azurelog.LogPackage(valueSource.registration.id, valueSource.version.ToString(), true, errorMessages, writer);//Call to the method to log the status of the package 
                        }
                    }

                    else
                    {
                        errorMessages.Add("Package found in MiniDB and not in source");
                        azurelog.LogPackage(entry.Value.registration.id, entry.Value.version.ToString(), true, errorMessages, writer);
                        azurelog.ReportDictionary.Add(entry.Value.registration.id + " " + entry.Value.version, errorMessages);
                    }                 
                }

                foreach (KeyValuePair<int,Packages> entry in source)
                {
                    List<string> errorMessages = new List<string>();

                    if (entry.Value.registration.id=="owin")
                    {
                        Console.WriteLine("Here");
                    }

                    Packages valueMiniDB=null;
                    if (!(miniDB.TryGetValue(entry.Key,out valueMiniDB)))
                    {
                       
                            errorMessages.Add("Package found in source and not in MiniDB");
                            azurelog.LogPackage(entry.Value.registration.id, entry.Value.version.ToString(), true, errorMessages, writer);
                            azurelog.ReportDictionary.Add(entry.Value.registration.id + " " + entry.Value.version, errorMessages);
                        
                    }
                }


                azurelog.HtmlRender(packageCount, totalTimeForRun);
                writer.WriteLine("Time ended: " + DateTime.Now);
            }

            return packageCount;


        }

        public static int SafeGetInt(SqlDataReader reader, string property)
        {
            int colIndex = reader.GetOrdinal(property);
            if (!reader.IsDBNull(colIndex))
                return reader.GetInt32(colIndex);
            else
                return default(Int32);
        }

        public static Dictionary<int, Packages> GetPackageDictionary(string connectionString, string SQLQuery, string keyDB)
        {
            /*Runs the sql script and retrieves the relevant columns and adds to the dictionary*/


            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(SQLQuery, connection);
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection);

                Dictionary<int, Packages> packageInfoList = new Dictionary<int, Packages>();
                if (reader != null)
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        Packages package = new Packages();

                        package.registration.id = reader["Id"].ToString().ToLower();
                        string downloadCount = reader["DownloadCount"].ToString();
                        package.registration.downloadCount = Convert.ToInt32(downloadCount);


                        int key = SafeGetInt(reader, keyDB);
                        package.packageKey = Convert.ToInt32(key);
                        package.description = reader["Description"].ToString();
                        package.title = reader["Title"].ToString().ToLower();
                        package.version = reader["Version"].ToString();
                        package.summary = reader["Summary"].ToString();
                        package.isLatest = Boolean.Parse(reader["IsLatest"].ToString());
                        package.isLatestStable = Boolean.Parse(reader["IsLatestStable"].ToString());
                        package.isPrerelease = Boolean.Parse(reader["IsPrerelease"].ToString());
                        package.requiresLicenseAcceptance = Boolean.Parse(reader["RequiresLicenseAcceptance"].ToString());
                        package.language = reader["Language"].ToString();
                        package.tags = reader["Tags"].ToString().TrimStart(' ').TrimEnd(' ').ToLower().Replace("\r\n", "").Replace("\\","").Replace('"', ' ').Replace(" ", "");
                        package.created = DateTime.Parse(reader["Created"].ToString());
                        package.published = DateTime.Parse(reader["Published"].ToString());
                        package.projectUrl = reader["ProjectUrl"].ToString();
                        package.licenseUrl = reader["LicenseUrl"].ToString();

                        var packageAuthorsKey = SafeGetInt(reader, "PackageKey");
                        package.authors.packageKey = Convert.ToInt32(packageAuthorsKey);
                        string author = reader["Name"].ToString().TrimStart(' ').TrimEnd(' ');


                        var packageFrameworksKey = SafeGetInt(reader, "packageKey");
                        package.frameworks.packageKey = Convert.ToInt32(packageFrameworksKey);
                        string targetFramework = reader["TargetFramework"].ToString().ToLower();

                        var packageDependenciesKey = SafeGetInt(reader, "PackageKey");
                        package.dependencies.packageKey = Convert.ToInt32(packageDependenciesKey);

                        string dependencyId = reader["dependencyId"].ToString().ToLower();
                        string dependencyFramework = reader["dependencyFramework"].ToString().ToLower();

                        int dictionaryKey = 0;

                        dictionaryKey = SafeGetInt(reader, keyDB);


                        Packages dictionaryValue = package;

                        if (packageInfoList.ContainsKey(dictionaryKey))
                        {
                            Packages dictionaryValueExistingKey;
                            packageInfoList.TryGetValue(dictionaryKey, out dictionaryValueExistingKey);
                            dictionaryValueExistingKey.authors.authorsList.Add(author.TrimStart(' ').TrimEnd(' '));
                            dictionaryValueExistingKey.frameworks.frameworksList.Add(targetFramework);
                            dictionaryValueExistingKey.dependencies.dependenciesList.Add(new Tuple<string, string>(dependencyId, dependencyFramework));
                        }

                        else
                        {
                            dictionaryValue.authors.authorsList.Add(author.TrimStart(' ').TrimEnd(' '));
                            dictionaryValue.frameworks.frameworksList.Add(targetFramework);
                            dictionaryValue.dependencies.dependenciesList.Add(new Tuple<string, string>(dependencyId, dependencyFramework));
                            packageInfoList.Add(dictionaryKey, dictionaryValue);
                            count++;
                        }
                    }

                    return packageInfoList;
                }

                return null;
            }
        }



        public bool Equals(Packages sourcePackage, Packages miniDBpackage, out string message)
        {
            //Checks if the metadata about each package was copied to the catalog correctly
            message = string.Empty;
            if (sourcePackage.packageKey != miniDBpackage.packageKey)
            {
                message += "packageKeys do not match; Table: Packages. ";

            }

            if (sourcePackage.description.Trim() != miniDBpackage.description.Trim())
            {
                message += "descriptions do not match; Table: Packages. ";
            }

            if (sourcePackage.title != miniDBpackage.title)
            {
                message += "titles do not match; Table: Packages. ";
            }

            if (sourcePackage.version+".0"!=miniDBpackage.version && sourcePackage.version != miniDBpackage.version && miniDBpackage.version+".0"!=sourcePackage.version)
            {
                message += "versions do not match; Table: Packages. ";

               
            }

            if (sourcePackage.summary != miniDBpackage.summary)
            {
                message += "summaries do not match; Table: Packages. ";
            }

            if (sourcePackage.releaseNotes != miniDBpackage.releaseNotes)
            {
                message += "release notes do not match; Table: Packages. ";
            }

            if (sourcePackage.language != miniDBpackage.language)
            {
                message += "language does not match; Table: Packages. ";
            }

            if (sourcePackage.tags != miniDBpackage.tags)
            {
                message += "tags do not match; Table: Packages. ";
            }

            if (sourcePackage.projectUrl != miniDBpackage.projectUrl)
            {
                message += "projectUrl does not match; Table: Packages. ";
            }

            if (sourcePackage.licenseUrl != miniDBpackage.licenseUrl)
            {
                message += "licenseUrl does not match; Table: Packages. ";

            }

            if (sourcePackage.isLatest != miniDBpackage.isLatest)
            {
                message += "isLatest does not match; Table: Packages. ";
            }

            if (sourcePackage.isLatestStable != miniDBpackage.isLatestStable)
            {
                message += "isLatestStable does not match; Table: Packages. ";
            }

            if (sourcePackage.created != miniDBpackage.created)
            {
                message += "created does not match; Table: Packages. ";
            }

            if (sourcePackage.published != miniDBpackage.published)
            {
                message += "published does not match; Table: Packages. ";
            }

            //if (sourcePackage.requiresLicenseAcceptance != miniDBpackage.requiresLicenseAcceptance)
            //{
            //    message += "requiresLicenseAcceptance does not match; Table: Packages. ";

            //}

            if (sourcePackage.isPrerelease != miniDBpackage.isPrerelease)
            {
                message += "isPrerelease does not match; Table: Packages. ";
            }

            else if (message==string.Empty)
            {
                message = "No error; Table: Packages.";
                return false;
            }

            return true;
        }

        public bool Equals(PackageRegistrations sourcePackage, PackageRegistrations miniDBpackage, out string message)
        {
            //Checks if the Id of the package was copied to the catalog correctly
            message = string.Empty;
            if (sourcePackage.id != miniDBpackage.id)
            {
                message = "Ids do not match; Table: PackageRegistrations.";
            }

            else if (message == string.Empty)
            {
                message = "No error; Table: PackageRegistrations.";
                return false;
            }

            return true;
        }

        public bool Equals(PackageFrameworks sourcePackage, PackageFrameworks miniDBpackage, out string message)
        {
            //Checks that each target framework available for the package is as expected
            message = string.Empty;
            foreach (string framework in sourcePackage.frameworksList)
            {
                if (!miniDBpackage.frameworksList.Contains(framework))
                {
                    message += "TargetFrameworks do not match; Table: PackageFrameworks. ";
                }
            }

            if (message == string.Empty)
            {
                message = "No error; Table: PackageFrameworks.";
                return false;
            }

            return true;
        }

        public bool Equals(PackageDependencies sourcePackage, PackageDependencies miniDBpackage, out string message)
        {
            //Checks if the dependencies of each package were copied correctly to the catalog
            message = string.Empty;

            if (sourcePackage.packageKey != miniDBpackage.packageKey)
            {
                message += "packageKeys do not match; Table: PackageDependencies. ";
            }

            foreach (Tuple<string, string> dependency in miniDBpackage.dependenciesList)
            {
                if (!sourcePackage.dependenciesList.Contains(dependency))
                {
                    message = "Dependency Ids do not match; Table: PackageDependencies. ";
                }
            }

            if (message == string.Empty)
            {
                message = "No error; Table: PackageDependencies";
                return false;
            }

            return true;
        }

        public bool Equals(PackageAuthors sourcePackage, PackageAuthors miniDBpackage, out string message)
        {
            //Checks if the authors of each package were copied correctly to the catalog
            message = string.Empty;

            //foreach (string author in sourcePackage.authorsList)
            //{
            //    author=author.TrimStart(' ').TrimEnd(' ').ToLower();
            //}

            if (sourcePackage.packageKey != miniDBpackage.packageKey)
            {
                message += "packageKeys do not match; Table: PackageAuthors. ";
            }

            foreach (string author in miniDBpackage.authorsList)
            {
                if (!sourcePackage.authorsList.Contains(author))
                {

                    message = "Name does not match; Table: PackageAuthors. ";

                }
            }

            if (message == string.Empty)
            {
                message = "No error; Table: PackageAuthors.";
                return false;
            }

            return true;
        }


    }
}
