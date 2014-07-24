using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
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
        bool Equals(Packages sourcePackage, Packages miniDBpackage, out string message);
        bool Equals(PackageRegistrations sourcePackage, PackageRegistrations miniDBpackage, out string message);
        bool Equals(PackageFrameworks sourcePackage, PackageFrameworks miniDBpackage, out string message);
        bool Equals(PackageDependencies sourcePackage, PackageDependencies miniDBpackage, out string message);
        
        //Called by DataBaseGenerator after creating the mini DB, to check for data integrity
        //Takes an input the connection strings to the source DB and the mini DB
        int ValidateDataIntegrity(string connectionStringSource, string connectionStringMiniDB, StreamWriter totalTimeForRun);

        //Compares the dictionaries created with data from each of the DBs
        int Compare(Dictionary<int, Packages> source, Dictionary<int, Packages> miniDB, StreamWriter totalTimeForRun);
    }
}
