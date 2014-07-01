using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CatalogTests
{
    class MyCatalogItem : CatalogItem
    {
        string _name;
        string _lastname;
        string _type;
        string _age;

        public MyCatalogItem(KeyValuePair<Tuple<string, string>, Tuple<string, string>> person)
        {
            _name = person.Key.Item1;
            _lastname = person.Key.Item2;
            _type = person.Value.Item1;
            _age = person.Value.Item2;
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            JObject obj = new JObject();
            obj.Add("name", _name);
            obj.Add("lastname", _lastname);
            obj.Add("type", _type);
            obj.Add("age", _age);//to add a property to the file, use obj.Add
            StorageContent content = new StringStorageContent(obj.ToString(), "application/json");

            return content;
        }

        protected override string GetItemIdentity()
        {
            return _name+" "+_lastname+" "+_age;//the file name it will be stored as 
        }

        public override Uri GetItemType()
        {
            return new Uri("http://tempuri.org/type#" + _type);
        }
    }

    class Program
    {
        static async Task WriteCatalog()
        {
            //Storage storage = new FileStorage
            //{
            //    Path = @"c:\CatalogDemo\test",
            //    Container = "test",
            //    BaseAddress = "http://localhost:8000"
            //};

            Storage storage = new AzureStorage
            {
                AccountKey = "Dmhc78jf+yfUcAOz/YZV8fVoDhPO4eJItKUiiqIfHj9dnif6dJZtGy30G+JK3/jp93yuJrLeaG1sae/wuoWyNw==",
                AccountName = "linked",
                Container = "demo",
                BaseAddress = "http://linked.blob.core.windows.net/"
            };

            CatalogContext context = new CatalogContext();

            CatalogWriter writer = new CatalogWriter(storage, context, 3, false);

            DateTime now = DateTime.UtcNow;

            int i = 0;

            //use tuples for multiple properties?
            IDictionary<Tuple<string, string>, Tuple<string, string>> band = new Dictionary<Tuple<string, string>, Tuple<string, string>>
            {              
                {new Tuple <string,string>("john","lennon"), new Tuple<string,string>("Singer", "30")},
                {new Tuple <string,string>("paul","mccartney"), new Tuple<string,string>("Singer", "30")}, 
                {new Tuple <string,string>("ringo","starr"), new Tuple<string,string>("Drummer", "30")}, 
                {new Tuple <string,string>("george","harrison"), new Tuple<string,string>("Guitarist", "30")},
                {new Tuple <string,string>("bonzo","bonham"), new Tuple<string,string>("Drummer", "30")}
            };

            foreach (var member in band)
            {
                writer.Add(new MyCatalogItem(member));
                await writer.Commit(now.AddMinutes(i++));
            }
        }

        static async Task ReadCatalog()
        {
            DateTime lastReadTime = DateTime.Parse("5/28/2014 9:04:10 PM");

            string baseAddress = "http://linked.blob.core.windows.net/demo/";
            //string baseAddress = "http://localhost:8000/test/";

            Uri address = new Uri(string.Format("{0}catalog/index.json", baseAddress));

            HttpClient client = new HttpClient();

            string indexJson = await client.GetStringAsync(address);
            JObject indexObj = JObject.Parse(indexJson);

            foreach (JToken indexItem in indexObj["item"])
            {
                DateTime indexItemTimeStamp = indexItem["timeStamp"]["@value"].ToObject<DateTime>();

                if (indexItemTimeStamp > lastReadTime)
                {
                    string pageJson = await client.GetStringAsync(indexItem["url"].ToObject<Uri>());
                    JObject pageObj = JObject.Parse(pageJson);

                    foreach (JToken pageItem in pageObj["item"])
                    {
                        DateTime pageItemTimeStamp = pageItem["timeStamp"]["@value"].ToObject<DateTime>();

                        if (pageItemTimeStamp > lastReadTime)
                        {

                            if (pageItem["@type"].ToString() == "http://tempuri.org/type#Singer" )
                            {
                                string dataJson = await client.GetStringAsync(pageItem["url"].ToObject<Uri>());
                                JObject dataObj = JObject.Parse(dataJson);

                                Console.WriteLine(dataObj["name"]);
                                Console.WriteLine(dataObj["lastname"]);
                                Console.WriteLine(dataObj["age"]);
                            }
                        }
                    }
                }
            }
        }

        static void PrintException(Exception e)
        {
            if (e is AggregateException)
            {
                foreach (Exception ex in ((AggregateException)e).InnerExceptions)
                {
                    PrintException(ex);
                }
            }
            else
            {
                Console.WriteLine("{0} {1}", e.GetType().Name, e.Message);
                Console.WriteLine("{0}", e.StackTrace);
                if (e.InnerException != null)
                {
                    PrintException(e.InnerException);
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                ExportTests.Test0();
                //WriteCatalog().Wait();
                //ReadCatalog().Wait();
            }
            catch (Exception e)
            {
                PrintException(e);
            }
        }
    }
}
