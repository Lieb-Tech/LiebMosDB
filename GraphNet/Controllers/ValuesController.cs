using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.Graphs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GraphNet.Controllers
{
    public class ValuesController : ApiController
    {
        static string endpoint = "https://liebmos.documents.azure.com:443";
        static string authKey = "1DSoCTY32l5O2Ixu5a3knwKx222b2n60gNw07vyv0WLdPfTX2Zjn4fJewxmnJn2s4FmFl3XjjgeavcxfR8RVJg==";
        static DocumentClient client = new DocumentClient(
                new Uri(endpoint),
                authKey,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp });
    
        [HttpGet]
        [Route("Search/{info}")]
        public async Task<IHttpActionResult> GetSearch(string info)
        {
            DocumentCollection graph = await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri("graphdb"),
                new DocumentCollection { Id = "Persons" },
                new RequestOptions { OfferThroughput = 400 });

            char firstChar = info.ToUpper()[0];
            char secondChar = (char)(firstChar + 1);

            string srch = info.ToUpper();

            string grem = $"g.V().has('Name', between('{firstChar}','{secondChar}')).values('id', 'Name')";
            IDocumentQuery<dynamic> query = client.CreateGremlinQuery<dynamic>(graph, grem);

            var results = new List<dynamic>();
            while (query.HasMoreResults)
            {
                string id = null;
                foreach (dynamic result in await query.ExecuteNextAsync())
                {
                    if (id == null)
                        id = result;
                    else
                    {
                        results.Add(new { id = id, name = result});
                        id = null;
                    }
                }
            }

            return Ok(new { results = results.Where(z => z.name.ToUpper().Contains(srch)).ToList() });
        }

        [HttpGet]
        [Route("Info/{info}")]
        public async Task<IHttpActionResult> GetInfo(string info)
        {
            DocumentCollection graph = await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri("graphdb"),
                new DocumentCollection { Id = "Persons" },
                new RequestOptions { OfferThroughput = 400 });
           
            string grem = $"g.V('{info}')";
            IDocumentQuery<dynamic> query = client.CreateGremlinQuery<dynamic>(graph, grem);
            var vert = new List<dynamic>();
            var eMe = new List<dynamic>();
            var eOther = new List<dynamic>();

            while (query.HasMoreResults)
            {                
                foreach (dynamic result in await query.ExecuteNextAsync())
                {
                    vert.Add(result);                        
                }
            }
            
            grem = $"g.V('{info}').outE()";
            query = client.CreateGremlinQuery<dynamic>(graph, grem);

            while (query.HasMoreResults)
            {
                string inV = "", label = "", outV = "";
                foreach (dynamic result in await query.ExecuteNextAsync())
                {
                    foreach (KeyValuePair<string, JToken> child in (result as JObject))
                    {
                        if (child.Key == "inV")
                            inV = (child.Value as JToken).Value<string>();
                        else if (child.Key == "label")
                            label = (child.Value as JToken).Value<string>();
                        else if (child.Key == "outV")
                            outV = (child.Value as JToken).Value<string>();
                    }
                    eMe.Add(new { outV, label, inV });
                }
            }
            grem = $"g.V('{info}').inE()";
            query = client.CreateGremlinQuery<dynamic>(graph, grem);

            while (query.HasMoreResults)
            {
                string inV = "", label = "", outV = "";
                foreach (dynamic result in await query.ExecuteNextAsync())
                {
                    foreach (KeyValuePair<string, JToken> child in (result as JObject))
                    {
                        if (child.Key == "inV")
                            inV = (child.Value as JToken).Value<string>();
                        else if (child.Key == "label")
                            label = (child.Value as JToken).Value<string>();
                        else if (child.Key == "outV")
                            outV = (child.Value as JToken).Value<string>();
                    }
                    eOther.Add(new { inV, label, outV });
                }
            }
            string marriedTo = "";
            List<dynamic> family = new List<dynamic>();

            if (eMe.Any(z => z.label == "Married"))
            {
                marriedTo = eMe.First(z => z.label == "Married").inV;
                eMe.Remove(eMe.First(z => z.label == "Married"));
            }
            else if (eOther.Any(z => z.label == "Married"))
            {
                marriedTo = eOther.First(z => z.label == "Married").outV;
                eOther.Remove(eOther.First(z => z.label == "Married"));
            }
            if (eMe.Any(z => z.label == "Father"))
            {
                family.AddRange(eMe.Where(z => z.label == "Father").Select(z => new { type = "Child", name = (string)z.inV }).ToList());
                eMe.RemoveAll((z) => { return z.label == "Father"; } );
            }
            else if (eMe.Any(z => z.label == "Mother"))
            {
                family.AddRange(eMe.Where(z => z.label == "Mother").Select(z => new { type = "Child", name = (string)z.inV }).ToList());
                eMe.RemoveAll((z) => { return z.label == "Mother"; });
            }

            if (eOther.Any(z => z.label == "Father"))
            {
                family.AddRange(eOther.Where(z => z.label == "Father").Select(z => new { type = "Father", name = (string)z.outV }).ToList());
                eOther.RemoveAll((z) => { return z.label == "Father"; });
            }
            if (eOther.Any(z => z.label == "Mother"))
            {
                family.AddRange(eOther.Where(z => z.label == "Mother").Select(z => new { type = "Mother", name = (string)z.outV }).ToList());
                eOther.RemoveAll((z) => { return z.label == "Mother"; });
            }

            if (eMe.Any(z => z.label == "Sister"))
            {
                family.AddRange(eMe.Where(z => z.label == "Sister").Select(z => new { type = "Sister", name = (string)z.outV }).ToList());
                eMe.RemoveAll((z) => { return z.label == "Sister"; });
            }
            else if (eMe.Any(z => z.label == "Brother"))
            {
                family.AddRange(eMe.Where(z => z.label == "Brother").Select(z => new { type = "Brother", name = (string)z.outV }).ToList());
                eMe.RemoveAll((z) => { return z.label == "Brother"; });
            }
            if (eOther.Any(z => z.label == "Sister"))
            {
                family.AddRange(eOther.Where(z => z.label == "Sister").Select(z => new { type = "Sister", name = (string)z.inV }).ToList());
                eOther.RemoveAll((z) => { return z.label == "Sister"; });
            }
            else if (eOther.Any(z => z.label == "Brother"))
            {
                family.AddRange(eOther.Where(z => z.label == "Brother").Select(z => new { type = "Brother", name = (string)z.inV }).ToList());
                eOther.RemoveAll((z) => { return z.label == "Brother"; });
            }

            return Ok(new { vert, eMe, eOther, marriedTo, family });
        }

        public class propertyInfo
        {            
            public string key;
            public string value;
        }
        public class edgeInfo
        {
            public string label;
            public string v;
        }

        [HttpGet]
        [Route("Path/{from}/{to}")]
        public async Task<IHttpActionResult> GetPath(string from, string to)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return BadRequest();

            DocumentCollection graph = await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri("graphdb"),
                new DocumentCollection { Id = "Persons" },
                new RequestOptions { OfferThroughput = 400 });

            var path = new List<string>();
            string grem = $"g.V('{from}').repeat(both('Father').simplePath()).until(has('person', 'Name', '{to}')).path().limit(1).unfold()";
            IDocumentQuery<dynamic> query = client.CreateGremlinQuery<dynamic>(graph, grem);
            while (query.HasMoreResults)
            {
                foreach (dynamic result in await query.ExecuteNextAsync())
                {
                    path.Add(result["id"].ToString());
                }
            }

            return Ok(new { path });
        }

        [HttpPost]
        [Route("AddEdge/{id}")]
        public async Task<IHttpActionResult> PostAddEdge(string id, edgeInfo info)
        {
            if (info == null)
                return BadRequest();

            DocumentCollection graph = await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri("graphdb"),
                new DocumentCollection { Id = "Persons" },
                new RequestOptions { OfferThroughput = 400 });

            string grem = $"g.V('{id}').count()";
            IDocumentQuery<dynamic> query = client.CreateGremlinQuery<dynamic>(graph, grem);
            while (query.HasMoreResults)
            {
                foreach (dynamic result in await query.ExecuteNextAsync())
                {
                    if (result == 0)
                        return NotFound();
                }
            }
            grem = $"g.V('{info.v}').count()";
            query = client.CreateGremlinQuery<dynamic>(graph, grem);
            while (query.HasMoreResults)
            {
                foreach (dynamic result in await query.ExecuteNextAsync())
                {
                    if (result == 0)
                    {
                        grem = $"g.addV('person').property('id', '{info.v}').property('Name', '{info.v}')";
                        query = client.CreateGremlinQuery<dynamic>(graph, grem);
                        await query.ExecuteNextAsync();
                    }
                }
            }

            grem = $"g.V('{id}').addE('{info.label}').to(g.V('{info.v}'))";
            query = client.CreateGremlinQuery<dynamic>(graph, grem);
            await query.ExecuteNextAsync();

            return Ok();
        }

        [HttpPost]
        [Route("AddProperty/{id}")]
        public async Task<IHttpActionResult> PostAddProperty(string id, propertyInfo info)
        {
            if (info == null)
                return BadRequest();

            DocumentCollection graph = await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri("graphdb"),
                new DocumentCollection { Id = "Persons" },
                new RequestOptions { OfferThroughput = 400 });

            string grem = $"g.V('{id}').count()";
            IDocumentQuery<dynamic> query = client.CreateGremlinQuery<dynamic>(graph, grem);
            while (query.HasMoreResults)
            {
                foreach (dynamic result in await query.ExecuteNextAsync())
                {
                    if (result == 0)
                        return NotFound();
                }
            }

            grem = $"g.V('{id}').property('{info.key}', '{info.value.Replace("'","''")}')";
            query = client.CreateGremlinQuery<dynamic>(graph, grem);
            await query.ExecuteNextAsync();

            return Ok();
        }

        // GET api/values
        public async Task<IEnumerable<string>> Get()
        {
            // await buildDB();

            return new List<string>();
        }

        public async Task<IEnumerable<string>> buildDB()
        {
            var results = new List<string>();
            Microsoft.Azure.Documents.Database database = await client.CreateDatabaseIfNotExistsAsync(new Database { Id = "graphdb" });

            DocumentCollection graph = await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri("graphdb"),
                new DocumentCollection { Id = "Persons" },
                new RequestOptions { OfferThroughput = 400 });

            Dictionary<string, string> gremlinQueries = new Dictionary<string, string>
            {
                { "Cleanup",        "g.V().drop()" },
                { "AddAdam",        "g.addV('person').property('id', 'Adam').property('Name', 'Adam')" },
                { "AddEve",         "g.addV('person').property('id', 'Eve').property('Name', 'Eve')" },
                { "AddCain",        "g.addV('person').property('id', 'Cain').property('Name', 'Cain')" },
                { "AddAbel",        "g.addV('person').property('id', 'Abel').property('Name', 'Abel')" },
                { "AddSeth",        "g.addV('person').property('id', 'Seth').property('Name', 'Seth')" },
                { "AddEnosh",        "g.addV('person').property('id', 'Enosh').property('Name', 'Enosh')" },

                { "AddAdam+Eve",      "g.V('Adam').addE('Married').to(g.V('Eve'))" },

                { "AddCain->Abel",      "g.V('Cain').addE('Brother').to(g.V('Abel'))" },
                { "AddCain->Seth",      "g.V('Cain').addE('Brother').to(g.V('Seth'))" },
                { "AddAbel->Seth",      "g.V('Abel').addE('Brother').to(g.V('Seth'))" },

                { "AddEve->Abel",      "g.V('Eve').addE('Mother').to(g.V('Abel'))" },
                { "AddEve->Cain",      "g.V('Eve').addE('Mother').to(g.V('Cain'))" },
                { "AddEve->Seth",      "g.V('Eve').addE('Mother').to(g.V('Seth'))" },

                { "AddAdam->Abel",      "g.V('Adam').addE('Father').to(g.V('Abel'))" },
                { "AddAdam->Cain",      "g.V('Adam').addE('Father').to(g.V('Cain'))" },
                { "AddAdam->Seth",      "g.V('Adam').addE('Father').to(g.V('Seth'))" },
                { "AddSeth->Enosh",      "g.V('Seth').addE('Father').to(g.V('Enosh'))" },

                { "UpdateAbel1",    "g.V('Abel').property('Profession', 'Shepherd')" },
                { "UpdateCain1",    "g.V('Cain').property('Profession', 'Farmer')" },

                { "UpdateAdam",     "g.V('Adam').property('Max Age', '930').property('Born', '4026 BCE').property('Died', '3096 BCE').property('Name Means', 'Earthling Man; Mankind; Humankind; from a root meaning \"red\"')" },
                { "UpdateAdamFatherAge" , "g.V('Adam').outE('Father').as('e').inV().has('Name', 'Seth').select('e').property('Age', '130')" }


            };

            foreach (KeyValuePair<string, string> gremlinQuery in gremlinQueries)
            {
                results.Add($"Running {gremlinQuery.Key}: {gremlinQuery.Value}");

                // The CreateGremlinQuery method extensions allow you to execute Gremlin queries and iterate
                // results asychronously
                IDocumentQuery<dynamic> query = client.CreateGremlinQuery<dynamic>(graph, gremlinQuery.Value);
                while (query.HasMoreResults)
                {
                    foreach (dynamic result in await query.ExecuteNextAsync())
                    {
                        results.Add($"\t {JsonConvert.SerializeObject(result)}");
                    }
                }

                results.Add("");
            }

            return results;
        }

        // GET api/values/5
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        public void Delete(int id)
        {
        }
    }
}
