using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
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
            Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors>> sourceDictionary = GetPackageDictionary(connectionStringSource);
            Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors>> miniDBDictionary = GetPackageDictionary(connectionStringMiniDB);
            Compare(sourceDictionary, miniDBDictionary);
        }


        public void Compare(Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors>> source, Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors>> miniDB)
        {
            /* Input: Dictionaries populated with data from source DB and mini DB
             Each package is checked for the integrity of metadata*/
            AzureLogger azurelog = new AzureLogger();//Report logger initialized
            System.IO.StreamWriter writer = new System.IO.StreamWriter(@"C:\TEMP\JsonReport.txt");//Where the report is logged
            foreach (KeyValuePair<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors>> entry in miniDB)
            {
                Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors> valueSource;
                if (source.TryGetValue(entry.Key, out valueSource))//find corresponding key in the source DB
                {
                    string messageRegistrations = "";
                    string messagePackages = "";
                    string messageFrameworks = "";
                    string messageDependencies = "";
                    string messageAuthors = "";
                    List<string> errorMessages = new List<string>();

                    messageRegistrations = Equals(valueSource.Item1, entry.Value.Item1);
                    messagePackages = Equals(valueSource.Item2, entry.Value.Item2);
                    messageFrameworks = Equals(valueSource.Item3, entry.Value.Item3);
                    messageDependencies = Equals(valueSource.Item4, entry.Value.Item4);
                    messageAuthors = Equals(valueSource.Item5, entry.Value.Item5);
                    errorMessages.Add(messageRegistrations);
                    errorMessages.Add(messagePackages);
                    errorMessages.Add(messageFrameworks);
                    errorMessages.Add(messageDependencies);
                    errorMessages.Add(messageAuthors);

                    azurelog.LogPackage(valueSource.Item1.id, valueSource.Item2.version, errorMessages, writer);//Call to the method to log the status of the package                   
                }
            }

            writer.Close();
        }

        public static Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors>> GetPackageDictionary(string connectionString)
        {
            /*Runs the sql script and retrieves the relevant columns and adds to the dictionary*/
            string sql = @"
               SELECT PackageRegistrations.[Id],PackageRegistrations.[DownloadCount],
                    Packages.[Key],Packages.Description,Packages.IconUrl,Packages.LicenseUrl,Packages.ProjectUrl,
                    Packages.Summary, Packages.Tags, Packages.Title, Packages.Version, Packages.ReleaseNotes, Packages.Language,
                    Packages.IsLatest,Packages.IsLatestStable,Packages.IsPrerelease,Packages.RequiresLicenseAcceptance,
                    Packages.Created,Packages.Published,
                    PackageAuthors.Name,
                    PackageFrameworks.TargetFramework,
                    PackageDependencies.[Id] as dependencyId,PackageDependencies.TargetFramework as dependencyFramework
                                   
                FROM Packages 

                INNER JOIN PackageRegistrations ON PackageRegistrations.[Key] = Packages.[PackageRegistrationKey]
                LEFT OUTER JOIN PackageDependencies ON Packages.[Key] = PackageDependencies.[PackageKey] 
				LEFT OUTER JOIN PackageAuthors ON Packages.[Key]=PackageAuthors.[PackageKey]            
				LEFT OUTER JOIN PackageFrameworks ON Packages.[Key] = PackageFrameworks.[Package_Key]
                ";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(sql, connection);
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection);

                Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors>> packageInfoList = new Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors>>();
                if (reader != null)
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        Packages package = new Packages();
                        PackageRegistrations packageRegistrations = new PackageRegistrations();
                        PackageFrameworks packageFrameworks = new PackageFrameworks();
                        PackageDependencies packageDependencies = new PackageDependencies();
                        PackageAuthors packageAuthors = new PackageAuthors();

                        packageRegistrations.id = reader["Id"].ToString().ToLower();
                        string downloadCount = reader["DownloadCount"].ToString();
                        packageRegistrations.downloadCount = Convert.ToInt32(downloadCount);

                        string key = reader["Key"].ToString();
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
                        package.tags = reader["Tags"].ToString();//change format of tags in MiniDB
                        package.created = DateTime.Parse(reader["Created"].ToString());
                        package.published = DateTime.Parse(reader["Published"].ToString());
                        package.projectUrl = reader["ProjectUrl"].ToString();
                        package.licenseUrl = reader["LicenseUrl"].ToString();

                        string packageKeyString = reader["Key"].ToString();
                        packageAuthors.packageKey = Convert.ToInt64(packageKeyString);
                        packageAuthors.name = reader["Name"].ToString();

                        packageFrameworks.packageKey = Convert.ToInt64(packageKeyString);
                        packageFrameworks.targetFramework = reader["TargetFramework"].ToString().ToLower();

                        packageDependencies.packageKey = packageFrameworks.packageKey;
                        packageDependencies.id = reader["dependencyId"].ToString().ToLower();
                        packageDependencies.targetFramework = reader["dependencyFramework"].ToString().ToLower();

                        int dictionaryKey = 0;
                        dictionaryKey = reader["Key"] as int? ?? default(int);
                        Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors> dictionaryValue = new Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors>(packageRegistrations, package, packageFrameworks, packageDependencies, packageAuthors);

                        if (packageInfoList.Keys.Contains(dictionaryKey))
                        {
                            Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies, PackageAuthors> dictionaryValueExistingKey;
                            packageInfoList.TryGetValue(dictionaryKey, out dictionaryValueExistingKey);
                            dictionaryValueExistingKey.Item3.frameworks.Add(packageFrameworks.targetFramework);
                            dictionaryValueExistingKey.Item4.dependencies.Add(new Tuple<string, string>(packageDependencies.id, packageDependencies.targetFramework));
                        }

                        else
                        {
                            dictionaryValue.Item3.frameworks.Add(packageFrameworks.targetFramework);
                            dictionaryValue.Item4.dependencies.Add(new Tuple<string, string>(packageDependencies.id, packageDependencies.targetFramework));
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

        public string Equals(PackageAuthors sourcePackage, PackageAuthors miniDBpackage)
        {
            //Checks if the authors of each package were copied correctly to the catalog
            string message = "";
            if (sourcePackage.packageKey != miniDBpackage.packageKey)
            {
                message += "packageKeys do not match; Table: PackageAuthors. ";
            }

            foreach (string author in sourcePackage.authorsList)
            {
                if (!miniDBpackage.authorsList.Contains(author.TrimStart()))
                {

                    message = "Name does not match; Table: PackageAuthors. ";

                }
            }


            if (message == "")
            {
                message = "No error; Table: PackageAuthors.";
            }

            return message;
        }


    }
}
