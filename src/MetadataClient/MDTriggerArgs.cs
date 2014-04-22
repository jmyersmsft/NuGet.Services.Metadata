using System;
using PowerArgs;
using System.Data.SqlClient;

namespace MetadataClient
{
    public class MDTriggerArgs : BlobStorageArgs
    {
        [ArgRequired]
        [ArgDescription("DB Connection String")]
        public string DBConnectionString { get; set; }

        [ArgShortcut("c")]
        [ArgDescription("Container name")]
        public string ContainerName { get; set; }

        [ArgShortcut("n")]
        [ArgDescription("Nupkg Url Format")]
        public string NupkgUrlFormat { get; set; }

        [ArgShortcut("m")]
        [ArgDescription("Max records to pull")]
        public int MaxRecords { get; set; }

        [ArgDescription("PushToCloud")]
        public bool PushToCloud { get; set; }

        [ArgDescription("UpdateTables")]
        public bool UpdateTables { get; set; }
    }
}
