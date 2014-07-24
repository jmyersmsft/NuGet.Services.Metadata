using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTestTool
{
    /*Represents the object being compared. Contains all the properties of JSON objects in the catalog */
    public class Packages
    {
        public int packageKey; 
        public string description;
        public string title;
        public string version;
        public string summary;
        public string releaseNotes;
        public string language;
        public string tags;
        public DateTime created;
        public DateTime published;
        public Boolean isLatest;
        public Boolean isLatestStable;
        public Boolean requiresLicenseAcceptance;
        public Boolean isPrerelease;
        public string iconUrl;
        public string projectUrl;
        public string licenseUrl;

        public PackageAuthors authors = new PackageAuthors();
        public PackageDependencies dependencies = new PackageDependencies();
        public PackageFrameworks frameworks = new PackageFrameworks();
        public PackageRegistrations registration = new PackageRegistrations();

    }

    /*Each class represents a TABLE on the DB on SQL. The properties that are on the catalog are parsed and loaded into the properties of these classes*/
    public class PackageRegistrations
    {
        public long key;//registration key
        public string id;//package id
        public int downloadCount;
    }

    public class PackageAuthors
    {
        public long packageKey;//registration key        
        public List<string> authorsList = new List<string>();//list of authors
    }

    public class PackageFrameworks
    {
        public long packageKey;
        public List<string> frameworksList=new List<string>();//list of the frameworks the package supports
    }

    public class PackageDependencies
    {
        public long packageKey;
        public List<Tuple<string, string>> dependenciesList=new List<Tuple<string,string>>();//list of dependencies of the package
        public string id;
        public string targetFramework;
    }



}
