using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;


namespace CatalogTestTool
{
    public class DBComparer : IComparer
    {
        /* Invoked by DataBaseGenerator after creating the miniDB
         Input: connection strings to the source DB and mini DB
         Calls helper function, Compare() to perform comparisons between the the two DBs*/
        public void ValidateDataIntegrity(string connectionStringSource, string connectionStringMiniDB)
        {
            /* In the following dictionaries, the Key consists of Package Key.
            The Value is a Tuple of the other metadata about each package. For class deinifitons, see PackageData.cs*/
            Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>> sourceDictionary = GetPackageDictionary(connectionStringSource);
            Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>> miniDBDictionary = GetPackageDictionary(connectionStringMiniDB);
            Compare(sourceDictionary, miniDBDictionary);          
        }


        public void Compare(Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>> source, Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>> miniDB)
        {
            /* Input: Dictionaries populated with data from source DB and mini DB
             Each package is checked for the integrity of metadata*/
            AzureLogger azurelog = new AzureLogger();//Report logger initialized
            System.IO.StreamWriter writer = new System.IO.StreamWriter(@"C:\TEMP\JsonReport.txt");//Where the report is logged
            foreach (KeyValuePair<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>> entry in miniDB)
            {              
                Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies> valueSource;
                if (source.TryGetValue(entry.Key, out valueSource))//find corresponding key in the source DB
                {
                    string messageRegistrations = "";
                    string messagePackages = "";
                    string messageFrameworks = "";
                    string messageDependencies = "";
                    List<string> errorMessages = new List<string>();

                    messageRegistrations = Equals(valueSource.Item1, entry.Value.Item1);
                    messagePackages = Equals(valueSource.Item2, entry.Value.Item2);
                    messageFrameworks = Equals(valueSource.Item3, entry.Value.Item3);
                    messageDependencies = Equals(valueSource.Item4, entry.Value.Item4);

                    errorMessages.Add(messageRegistrations);
                    errorMessages.Add(messagePackages);
                    errorMessages.Add(messageFrameworks);
                    errorMessages.Add(messageDependencies);

                    azurelog.LogPackage(valueSource.Item1.id, valueSource.Item2.version, errorMessages, writer);//Call to the method to log the status of the package                   
                }
            }
        }

        public static Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>> GetPackageDictionary(string connectionString)
        {
            /*Runs the sql script and retrieves the relevant columns and adds to the dictionary*/
            string sql = @"
               	 SELECT PackageRegistrations.[Id],PackageRegistrations.[DownloadCount],
                    Packages.[Key],Packages.Description,Packages.IconUrl,Packages.LicenseUrl,Packages.ProjectUrl,
                    Packages.Summary, Packages.Tags, Packages.Title, Packages.Version, Packages.ReleaseNotes, Packages.Language,
                    PackageFrameworks.[Package_Key],PackageFrameworks.TargetFramework,
                    PackageDependencies.[PackageKey], PackageDependencies.[Id] as dependencyId,PackageDependencies.TargetFramework as dependencyFramework
                    
                       
                FROM Packages 

                INNER JOIN PackageRegistrations ON PackageRegistrations.[Key] = Packages.[PackageRegistrationKey] 
				
                LEFT OUTER JOIN PackageDependencies ON Packages.[Key] = PackageDependencies.[PackageKey]
				LEFT OUTER JOIN PackageFrameworks ON Packages.[Key] = PackageFrameworks.[Package_Key]

                ";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection);

                Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>> packageInfoList = new Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>>();
                if (reader != null)
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        Packages package = new Packages();
                        PackageRegistrations packageRegistrations = new PackageRegistrations();
                        PackageFrameworks packageFrameworks = new PackageFrameworks();
                        PackageDependencies packageDependencies = new PackageDependencies();

                        packageRegistrations.id = reader["Id"].ToString();
                        string downloadCount = reader["DownloadCount"].ToString();
                        packageRegistrations.downloadCount = Convert.ToInt32(downloadCount);

                        string key = reader["Key"].ToString();
                        package.packageKey = Convert.ToInt32(key);
                        package.description = reader["Description"].ToString();
                        package.title = reader["Title"].ToString();
                        package.version = reader["Version"].ToString();
                        package.summary = reader["Summary"].ToString();
                        package.releaseNotes = reader["ReleaseNotes"].ToString();
                        package.language = reader["Language"].ToString();
                        package.tags = reader["Tags"].ToString();//change format of tags in MiniDB
                        package.iconUrl = reader["IconUrl"].ToString();
                        package.projectUrl = reader["ProjectUrl"].ToString();
                        package.licenseUrl = reader["LicenseUrl"].ToString();

                        packageFrameworks.packageKey = reader["Package_Key"] as int? ?? default(int);
                        packageFrameworks.targetFramework = reader["TargetFramework"].ToString();

                        packageDependencies.packageKey = packageFrameworks.packageKey;
                        packageDependencies.id = reader["dependencyId"].ToString().ToLower();
                        packageDependencies.targetFramework = reader["dependencyFramework"].ToString();

                        int dictionaryKey = 0;
                        dictionaryKey = reader["Key"] as int? ?? default(int);
                        Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies> dictionaryValue = new Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>(packageRegistrations, package, packageFrameworks, packageDependencies);

                        if (packageInfoList.Keys.Contains(dictionaryKey))
                        {
                            Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies> dictionaryValueExistingKey;
                            packageInfoList.TryGetValue(dictionaryKey, out dictionaryValueExistingKey);
                            dictionaryValueExistingKey.Item3.frameworks.Add(packageFrameworks.targetFramework);
                            dictionaryValueExistingKey.Item4.dependencies.Add(new Tuple<string, string>(packageDependencies.id, packageDependencies.targetFramework));
                        }

                        else
                        {
                            packageInfoList.Add(dictionaryKey, dictionaryValue);
                            count++;
                        }
                    }

                    return packageInfoList;
                }

                return null;
            }
        }

        public string Equals(Packages sourcePackage, Packages miniDBpackage)
        {
            //Checks if the metadata about each package was copied to the catalog correctly
            string message = "";
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

            if (sourcePackage.version != miniDBpackage.version)
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

            if (sourcePackage.iconUrl != miniDBpackage.iconUrl)
            {
                message += "iconUrl does not match; Table: Packages. ";

            }

            if (sourcePackage.licenseUrl != miniDBpackage.licenseUrl)
            {
                message += "licenseUrl does not match; Table: Packages. ";

            }

            else
            {
                message = "No error; Table: Packages.";
            }

            return message;
        }

        public string Equals(PackageRegistrations sourcePackage, PackageRegistrations miniDBpackage)
        {
            //Checks if the Id of the package was copied to the catalog correctly
            string message = "";
            if (sourcePackage.id != miniDBpackage.id)
            {
                message = "Ids do not match; Table: PackageRegistrations.";
            }

            else message = "No error; Table: PackageRegistrations.";
            return message;
        }

        public string Equals(PackageFrameworks sourcePackage, PackageFrameworks miniDBpackage)
        {
            //Checks that each target framework available for the package is as expected
            string message = "";
            foreach (string framework in sourcePackage.frameworks)
            {
                if (!miniDBpackage.frameworks.Contains(framework))
                {
                    message += "TargetFrameworks do not match; Table: PackageFrameworks. ";
                }
            }

            if (message == "")
            {
                message = "No error; Table: PackageFrameworks.";
            }

            return message;
        }

        public string Equals(PackageDependencies sourcePackage, PackageDependencies miniDBpackage)
        {
            //Checks if the dependencies of each package were copied correctly to the catalog
            string message = "";
            if (sourcePackage.packageKey != miniDBpackage.packageKey)
            {
                message += "packageKeys do not match; Table: PackageDependencies. ";
            }

            foreach (Tuple<string, string> dependency in sourcePackage.dependencies)
            {
                if (!miniDBpackage.dependencies.Contains(dependency))
                {
                    message = "Dependency Ids do not match; Table: PackageDependencies. ";
                }
            }

            if (message == "")
            {
                message = "No error; Table: PackageDependencies";
            }

            return message;
        }
    }
}
