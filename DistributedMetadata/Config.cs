using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.DistributedWork
{
    public class Config
    {
        private const string File = "config.json";
        private readonly JObject _config;

        public Config()
        {
            FileInfo file = new FileInfo(File);

            using (var stream = file.OpenText())
            {
                _config = JObject.Parse(stream.ReadToEnd());
            }
        }

        private static Config _instance;
        public Config Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Config();
                }

                return _instance;
            }
        }

        public string GetProperty(string key)
        {
            JToken token = null;
            if (_config.TryGetValue(key, out token))
            {
                if (token.Type == JTokenType.Property)
                {
                    JProperty prop = (JProperty)token;
                    return prop.Value.ToString();
                }
            }

            return string.Empty;
        }

        public JObject Json
        {
            get
            {
                return _config;
            }
        }
    }
}
