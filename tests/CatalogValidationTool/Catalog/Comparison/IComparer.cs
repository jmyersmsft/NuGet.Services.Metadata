using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;


namespace CatalogTestTool
{
    interface IComparer
    {
        /* Equals method for each table in the database.
         * Input: Object of the table type from the source and the miniDB
         * Output: string of error messages*/
        string Equals(Packages sourcePackage, Packages miniDBpackage);
        string Equals(PackageRegistrations sourcePackage, PackageRegistrations miniDBpackage);
        string Equals(PackageFrameworks sourcePackage, PackageFrameworks miniDBpackage);
        string Equals(PackageDependencies sourcePackage, PackageDependencies miniDBpackage);

        //Called by DataBaseGenerator after creating the mini DB, to check for data integrity
        //Takes an input the connection strings to the source DB and the mini DB
        void ValidateDataIntegrity(string connectionStringSource, string connectionStringMiniDB);

        //Compares the dictionaries created with data from each of the DBs
        void Compare(Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>> source, Dictionary<int, Tuple<PackageRegistrations, Packages, PackageFrameworks, PackageDependencies>> miniDB);
    }
}
