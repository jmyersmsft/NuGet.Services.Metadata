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

        public static void RunScripts()
        {
            try
            {
                //connect to the CatalogTest DB which is the miniDB created from the catalog
                string sqlConnectionString = ConfigurationManager.AppSettings["MiniDBLocal"];

                SqlConnection connection = new SqlConnection(sqlConnectionString);
                connection.Open();

                //Run Scripts to create the tables in the miniDB
                FileInfo PackageAuthors = new FileInfo("PackageAuthors.sql");
                FileInfo Packages = new FileInfo("Packages.sql");

                FileInfo PackageRegistrations = new FileInfo("PackageRegistrations.sql");
                FileInfo PackageDependencies = new FileInfo("PackageDependencies.sql");
                FileInfo PackageFramework = new FileInfo("PackageFramework.sql");


                string PackageAuthorsScript = PackageAuthors.OpenText().ReadToEnd();
                string PackagesScript = Packages.OpenText().ReadToEnd();
                string PackageRegistrationsScript = PackageRegistrations.OpenText().ReadToEnd();
                string PackageDependenciesScript = PackageDependencies.OpenText().ReadToEnd();
                string PackageFrameworksScript = PackageFramework.OpenText().ReadToEnd();

                SqlCommand packageAuthors = new SqlCommand(PackageAuthorsScript, connection);
                SqlCommand packages = new SqlCommand(PackagesScript, connection);
                SqlCommand packageRegistrations = new SqlCommand(PackageRegistrationsScript, connection);
                SqlCommand packageDependencies = new SqlCommand(PackageDependenciesScript, connection);
                SqlCommand packageFrameworks = new SqlCommand(PackageFrameworksScript, connection);

                ExecuteNonQueryWithRetry(packageRegistrations);
                ExecuteNonQueryWithRetry(packages);
                ExecuteNonQueryWithRetry(packageAuthors);
                ExecuteNonQueryWithRetry(packageDependencies);
                ExecuteNonQueryWithRetry(packageFrameworks);

                //Server server = new Server(new ServerConnection(connection));
                //server.ConnectionContext.ExecuteNonQuery(PackageRegistrationsScript);
                //server.ConnectionContext.ExecuteNonQuery(PackagesScript);
                //server.ConnectionContext.ExecuteNonQuery(PackageDependenciesScript);
                //server.ConnectionContext.ExecuteNonQuery(PackageFrameworksScript);
                //server.ConnectionContext.ExecuteNonQuery(PackageAuthorsScript);


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
