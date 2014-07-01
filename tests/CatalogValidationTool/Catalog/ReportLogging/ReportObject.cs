using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CatalogTestTool
{
    /*JSON object used to create an error log */
    [DataContract]
    public class ReportObject
    {
        [DataMember]
        internal string id;//Id of the package

        [DataMember]
        internal string version;//Version of the package

        [DataMember]
        internal List<string> errorMessages;//error report of all errors found

        internal ReportObject(string id, string version, List<string> errorMessages)//object with these json properties
        {
            this.id = id;
            this.version = version;
            this.errorMessages = errorMessages;
        }
    }  
}



