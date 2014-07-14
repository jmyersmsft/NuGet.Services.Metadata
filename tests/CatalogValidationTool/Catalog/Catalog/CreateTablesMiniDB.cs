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
        public static void RunScripts()
        {
            try
            {
                //connect to the CatalogTest DB which is the miniDB created from the catalog
                string sqlConnectionString = ConfigurationManager.AppSettings["MiniDBConnectionString"];

                SqlConnection connection = new SqlConnection(sqlConnectionString);
                connection.Open();

                //Run Scripts to create the tables in the miniDB
                FileInfo PackageAuthors = new FileInfo("PackageAuthors.sql");
                FileInfo Packages = new FileInfo("Packages.sql");
                
                FileInfo PackageRegistrations = new FileInfo("PackageRegistrations.sql");
                FileInfo PackageDependencies = new FileInfo("PackageDependencies.sql");
                FileInfo PackageFramework = new FileInfo("PackageFramework.sql");
                FileInfo Settings = new FileInfo("Settings.sql");

                string PackageAuthorsScript = PackageAuthors.OpenText().ReadToEnd();
                string PackagesScript = Packages.OpenText().ReadToEnd();
                string PackageRegistrationsScript = PackageRegistrations.OpenText().ReadToEnd();
                string PackageDependenciesScript = PackageDependencies.OpenText().ReadToEnd();
                string PackageFrameworkScript = PackageFramework.OpenText().ReadToEnd();
                string SettingsScript = Settings.OpenText().ReadToEnd();

                Server server = new Server(new ServerConnection(connection));
                server.ConnectionContext.ExecuteNonQuery(PackageRegistrationsScript);
                server.ConnectionContext.ExecuteNonQuery(PackagesScript);
                server.ConnectionContext.ExecuteNonQuery(PackageDependenciesScript);
                server.ConnectionContext.ExecuteNonQuery(PackageFrameworkScript);
                server.ConnectionContext.ExecuteNonQuery(PackageAuthorsScript);
                

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
