using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Gremlin.Net.Driver;
using Newtonsoft.Json;

namespace GraphNet.Controllers
{
    public class TinkerHelper
    {
        static string endpoint = "https://liebmos.documents.azure.com:443";        
        // static string hostname = "liebmos.gremlin.cosmosdb.azure.com";
        
        static string authKey = "1DSoCTY32l5O2Ixu5a3knwKx222b2n60gNw07vyv0WLdPfTX2Zjn4fJewxmnJn2s4FmFl3XjjgeavcxfR8RVJg==";

        static string hostname = "liebmos.gremlin.cosmosdb.azure.com";
        private static string database = "graphdb";
        private static string collection = "Persons2";
        static int port = 443;
           
        static GremlinServer gremlinServer = new GremlinServer(hostname, port, enableSsl: true,
                                                username: $"/dbs/{database}/colls/{collection}",
                                                password: authKey);
        static GremlinClient gremlinClient = new GremlinClient(gremlinServer);

        public async Task<string> ProcessCommand(string query)
        {
            var result = await gremlinClient.SubmitAsync<dynamic>(query);
            string output = JsonConvert.SerializeObject(result);
            return output;
        }
    }
}