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

        public static void CreateDatabaseAndTables()
        {
            try
            {
                //connect to the CatalogTest DB which is the miniDB created from the catalog
                string sqlConnectionString = ConfigurationManager.AppSettings["DBEngine"];

                SqlConnection connection = new SqlConnection(sqlConnectionString);
                connection.Open();
                string createDBString = String.Format("IF EXISTS (SELECT * FROM master.dbo.sysdatabases WHERE [name] = '{0}' ) ALTER DATABASE {0} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {0}; CREATE DATABASE {0};",ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);

                SqlCommand createDB = new SqlCommand(createDBString, connection);
                createDB.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                ExecuteNonQueryWithRetry(createDB);

                //Run Scripts to create the tables in the miniDB
                FileInfo PackageAuthors = new FileInfo("PackageAuthors.sql");
                FileInfo Packages = new FileInfo("Packages.sql");

                FileInfo PackageRegistrations = new FileInfo("PackageRegistrations.sql");
                FileInfo PackageDependencies = new FileInfo("PackageDependencies.sql");
                FileInfo PackageFramework = new FileInfo("PackageFramework.sql");

                string PackageAuthorsScript = "USE " + ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"] + PackageAuthors.OpenText().ReadToEnd();
                string PackagesScript = "USE " + ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"] + Packages.OpenText().ReadToEnd();
                string PackageRegistrationsScript = "USE " + ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"] + PackageRegistrations.OpenText().ReadToEnd();
                string PackageDependenciesScript = "USE " + ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"] + PackageDependencies.OpenText().ReadToEnd();
                string PackageFrameworksScript = "USE " + ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"] + PackageFramework.OpenText().ReadToEnd();

                SqlCommand packageAuthors = new SqlCommand(PackageAuthorsScript, connection);
                packageAuthors.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                SqlCommand packages = new SqlCommand(PackagesScript, connection);
                packages.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                SqlCommand packageRegistrations = new SqlCommand(PackageRegistrationsScript, connection);
                packageRegistrations.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                SqlCommand packageDependencies = new SqlCommand(PackageDependenciesScript, connection);
                packageDependencies.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
                SqlCommand packageFrameworks = new SqlCommand(PackageFrameworksScript, connection);
                packageFrameworks.Parameters.AddWithValue("@DBName", ConfigurationManager.AppSettings["MiniDBCreateDatabaseAndTables"]);
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
