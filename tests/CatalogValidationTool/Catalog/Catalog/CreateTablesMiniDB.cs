using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Configuration;

namespace CatalogTestTool
{
    class CreateTablesMiniDB
    {
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

        public static string GetDbName(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            return connectionStringBuilder.InitialCatalog;
        }


        public static void CreateDatabaseAndTables()
        {
            try
            {
                //connect to the CatalogTest DB which is the miniDB created from the catalog
                string sqlConnectionString = ConfigurationManager.AppSettings["DBEngine"];

                SqlConnection connection = new SqlConnection(sqlConnectionString);
                connection.Open();
                string MiniDBName = GetDbName(ConfigurationManager.AppSettings["MiniDBConnectionString"]);
                string createDBString = String.Format("IF EXISTS (SELECT * FROM master.dbo.sysdatabases WHERE [name] = '{0}' ) ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {0}; CREATE DATABASE {0};", MiniDBName);

                SqlCommand createDB = new SqlCommand(createDBString, connection);
                ExecuteNonQueryWithRetry(createDB);

                //Run Scripts to create the tables in the miniDB
                FileInfo PackageAuthors = new FileInfo("PackageAuthors.sql");
                FileInfo Packages = new FileInfo("Packages.sql");

                FileInfo PackageRegistrations = new FileInfo("PackageRegistrations.sql");
                FileInfo PackageDependencies = new FileInfo("PackageDependencies.sql");
                FileInfo PackageFramework = new FileInfo("PackageFramework.sql");

                string PackageAuthorsScript = "USE " + MiniDBName+" " + PackageAuthors.OpenText().ReadToEnd();
                string PackagesScript = "USE " + MiniDBName + "; " + Packages.OpenText().ReadToEnd();
                string PackageRegistrationsScript = "USE " + MiniDBName + " " + PackageRegistrations.OpenText().ReadToEnd();
                string PackageDependenciesScript = "USE " + MiniDBName + " " + PackageDependencies.OpenText().ReadToEnd();
                string PackageFrameworksScript = "USE " + MiniDBName + " " + PackageFramework.OpenText().ReadToEnd();

                SqlCommand packageAuthors = new SqlCommand(PackageAuthorsScript, connection);
                //packageAuthors.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                SqlCommand packages = new SqlCommand(PackagesScript, connection);
                //packages.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                SqlCommand packageRegistrations = new SqlCommand(PackageRegistrationsScript, connection);
                //packageRegistrations.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                SqlCommand packageDependencies = new SqlCommand(PackageDependenciesScript, connection);
                //packageDependencies.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                SqlCommand packageFrameworks = new SqlCommand(PackageFrameworksScript, connection);
                //packageFrameworks.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                ExecuteNonQueryWithRetry(packageRegistrations);
                ExecuteNonQueryWithRetry(packages);
                ExecuteNonQueryWithRetry(packageAuthors);
                ExecuteNonQueryWithRetry(packageDependencies);
                ExecuteNonQueryWithRetry(packageFrameworks);

                connection.Close();
            }

            catch (SqlException sqlEx)
            {
                Console.WriteLine(sqlEx.Message);
            }

            catch (System.Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
