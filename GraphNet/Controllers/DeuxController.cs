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
    public class DeuxController : ApiController
    {
        [HttpGet]
        [Route("Test")]
        public async Task<IHttpActionResult> GetTest()
        {
            var th = new TinkerHelper();
            var z = await th.ProcessCommand("g.V()");
            return Ok(z);
        }

        public List<keyValue> processList(string val)
        {
            val = val.Replace("\"","").Replace("\r\n","").Replace("{","").Replace("}","");
            var vals = val.Split(new char[] { ',' }).ToList();            
            var ret = new List<keyValue>();
            foreach (var v in vals)
            {
                var idx = v.IndexOf(":");
                ret.Add(new keyValue() { key = v.Substring(0, idx).Trim(), value = v.Substring(idx + 1).Trim() });
            }
            return ret.OrderByDescending(z => long.Parse(z.value)).Take(10).ToList();
        }
        [HttpGet]
        [Route("DBStats")]
        public async Task<IHttpActionResult> GetDBStats()
        {
            var grem = new GremlinHelper();
            var vert = (await grem.getPassthroughResult("g.V().count()")).First();
            var vertTypes = (await grem.getPassthroughResult("g.V().groupCount().by('label')")).First().ToString();
            var edge = (await grem.getPassthroughResult("g.E().count()")).First();
            var edgeTypes = (await grem.getPassthroughResult("g.E().groupCount().by('label')")).First().ToString();
            return Ok(new { edge, edgeTypes = processList(edgeTypes), vert, vertTypes = processList(vertTypes) });
        }

        [HttpGet]
        [Route("idByName/{name}")]
        public async Task<IHttpActionResult> GetIdByName(string name)
        {
            var grem = new GremlinHelper();
            var persons = await grem.getIdsByNameAsync(name);

            var results = new List<person>();
            foreach (var p in persons)
            {
                results.Add(await getById(p));
            }

            return Ok(new { results = results });
        }

        protected List<keyValue> getProperties(JToken info)
        {
            var lst = new List<keyValue>();
            foreach (var p in info["properties"])
            {
                if (p.Path != "properties.name")
                {
                    var idx = p.Path.IndexOf(".");
                    if (p.Path.StartsWith("properties["))
                    {
                        idx = p.Path.IndexOf("[");
                        lst.Add(new keyValue() { key = p.Path.Substring(idx + 2, p.Path.Length - idx - 4), value = p.Values()["value"].First().ToString() });
                    }
                    else
                        lst.Add(new keyValue() { key = p.Path.Substring(idx + 1), value = p.Values()["value"].First().ToString() });
                }
            }

            return lst;
        }

        public class keyValue
        {
            public keyValue(string key, string value)
            {
                this.key = key;
                this.value = value;
            }
            public keyValue()
            {

            }
            public string key;
            public string value;
        }
        public class person
        {
            public string name;
            public string id;
            public string type;
            public string gender;
            public List<person> spouse = new List<person>();
            public List<person> children = new List<person>();
            public List<person> parents = new List<person>();
            public List<person> siblings = new List<person>();
            public List<keyValue> properties = new List<keyValue>();
            public List<keyValue> livedIn = new List<keyValue>();
        }

        public async Task<person> getById(string id)
        {
            var grem = new GremlinHelper();
            var qry = $"g.V('{id}')";
            var info = await grem.getResultAsync(qry);

            var person = new person()
            {
                id = id,
            };

            var n = info["properties"].Where(x => x.Path == "properties.name").FirstOrDefault();
            if (n != null)
                person.name = n.Values()["value"].First().ToString();

            n = info["properties"].Where(x => x.Path == "properties.gender").FirstOrDefault();
            if (n != null)
                person.gender = n.Values()["value"].First().ToString();

            person.properties = getProperties(info);

            List<Task> tasks = new List<Task>();
            tasks.Add(Task.Run(async () =>
            {
                // my spouse
                qry = $"g.V('{id}').as('o').bothE('married').bothV().where(neq('o'))";
                var infoes = await grem.getResultsAsync(qry);
                foreach (var i in infoes)
                {
                    var p = new person()
                    {
                        name = (i as JToken)["properties"].Where(x => x.Path == "properties.name").First().Values().First()["value"].ToString(),
                        id = (i as JObject)["id"].ToString()
                    };

                    n = (i as JToken)["properties"].Where(x => x.Path == "properties.gender").FirstOrDefault();
                    if (n != null)
                    {
                        p.gender = n.Values()["value"].First().ToString();
                        p.type = person.gender == "F" ? "Husband" : "Wife";
                    }

                    person.spouse.Add(p);
                }
            }));

            tasks.Add(Task.Run(async () =>
            {
                // where I lived
                qry = $"g.V('{id}').outE('lived').inV()";
                var infoes = await grem.getResultsAsync(qry);
                foreach (var i in infoes)
                {
                    var vId = (i as JObject)["id"].ToString();
                    var name = (i as JToken)["properties"].Where(x => x.Path == "properties.name").First().Values().First()["value"].ToString();
                    person.livedIn.Add(new keyValue(vId, name));
                }
            }));

            tasks.Add(Task.Run(async () =>
            {
                // my children
                qry = $"g.V('{id}').outE('parent').inV()";
                var infoes = await grem.getResultsAsync(qry);
                var edges = await grem.getResultsAsync($"g.V('{id}').outE('parent')");
                foreach (var i in infoes)
                {
                    var c = new person()
                    {
                        name = (i as JToken)["properties"].Where(x => x.Path == "properties.name").First().Values().First()["value"].ToString(),
                        id = (i as JObject)["id"].ToString()
                    };

                    var g = (i as JToken)["properties"].Where(x => x.Path == "properties.gender").FirstOrDefault();
                    if (g != null)
                    {
                        c.gender = g.Values()["value"].First().ToString();
                        c.type = c.gender == "F" ? "Daughter" : "Son";
                    }

                    var child = edges.Where(x => (x as JObject)["inV"].ToString() == c.id).FirstOrDefault();
                    if (child != null)
                    {
                        var prop = (child as JObject).Children().FirstOrDefault(z => z.Path == "properties");
                        if (prop != null)
                        {
                            foreach (var t in prop.Values().Where(z => z.Path != "properties.type").ToList())
                            {
                                c.properties.Add(new keyValue((t as JProperty).Name, t.First().ToString()));
                            }
                        }
                    }

                    person.children.Add(c);
                }
            }));

            tasks.Add(Task.Run(async () =>
            {
                // my parents
                qry = $"g.V('{id}').inE('parent').outV()";
                var infoes = await grem.getResultsAsync(qry);
                var edges = await grem.getResultsAsync($"g.V('{id}').inE('parent')");
                foreach (var i in infoes)
                {
                    var p = new person()
                    {
                        name = (i as JToken)["properties"].Where(x => x.Path == "properties.name").First().Values().First()["value"].ToString(),
                        id = (i as JObject)["id"].ToString()
                    };

                    var par = edges.Where(x => (x as JObject)["outV"].ToString() == p.id).FirstOrDefault();
                    if (par != null)
                    {
                        var prop = (par as JObject).Children().FirstOrDefault(z => z.Path == "properties");
                        if (prop != null)
                        {
                            var t = prop.Values().FirstOrDefault(z => z.Path == "properties.type");
                            if (t != null)
                                p.type = t.First().ToString();
                        }
                    }

                    person.parents.Add(p);
                }
            }));

            tasks.Add(Task.Run(async () =>
            {
                // my siblings
                //qry = $"g.V('{id}').as('o').bothE('sibling').bothV().where(neq('o'))";
                qry = $"g.V('{id}').as('o').inE('parent').outV().outE('parent').inV().where(neq('o')).dedup()";
                var infoes = await grem.getResultsAsync(qry);
                foreach (var i in infoes)
                {
                    var sibId = (i as JObject)["id"].ToString();
                    if (sibId != id && !person.siblings.Any(x => x.id == id))
                    {
                        var s = new person()
                        {
                            name = (i as JToken)["properties"].Where(x => x.Path == "properties.name").First().Values().First()["value"].ToString(),
                            id = sibId
                        };
                        var g = (i as JToken)["properties"].Where(x => x.Path == "properties.gender").FirstOrDefault();
                        if (g != null)
                        {
                            if (g.Values()["value"].First().ToString() == "M")
                                s.type = "Brother";
                            else
                                s.type = "Sister";
                        }
                        person.siblings.Add(s);
                    }
                }
            }));

            Task.WaitAll(tasks.ToArray());
            return person;
        }

        [HttpGet]
        [Route("nameCheck/{name}")]
        public async Task<IHttpActionResult> GetNameCheck(string name)
        {
            var g = new GremlinHelper();
            string getEntries = $"g.V().has('person', 'name', '{name.Replace("'", "''")}')";
            var entries = await g.getResultsAsync(getEntries);

            if (!entries.Any())
                return Ok(new List<string>());

            var ret = new List<object>();

            foreach (var entry in entries)
            {
                string id = (entry as JObject)["id"].ToString();
                string upDown = $"g.V('{id}').as('o').bothE('parent').bothV().path().unfold().where(neq('o')).dedup()";
                var results = await g.getResultsAsync(upDown);

                string parents = "";
                string children = "";

                var v = new List<JObject>();
                var e = new List<JObject>();
                foreach (var r in results)
                {
                    if ((r as JObject)["label"].ToString() == "person")
                        v.Add(r);
                    else
                        e.Add(r);
                }

                var vIn = e.Select(x => x["inV"].ToString()).ToList();
                vIn.AddRange(e.Select(x => x["outV"].ToString()).ToList());
                var rr = vIn.GroupBy(z => z.ToString()).Select(z => z.ToList()).OrderByDescending(z => z.Count()).FirstOrDefault();
                var me = id;
                
                foreach (var r in e.Where(z => z["outV"].ToString() == me))
                {
                    var childId = r["inV"].ToString();
                    var child = v.Where(z => z["id"].ToString() == childId).First();
                    var childName = ((child as JObject)["properties"].Where(z => z.Path == "properties.name").First() as JToken).Values().First()["value"].ToString();
                    children += childName + ", ";
                }
                if (!string.IsNullOrWhiteSpace(children))
                    children = children.Substring(0, children.Length - 2);

                foreach (var r in e.Where(z => z["inV"].ToString() == me))
                {
                    var parentId = r["outV"].ToString();
                    var parent = v.Where(z => z["id"].ToString() == parentId).First();
                    var parentName = ((parent as JObject)["properties"].Where(z => z.Path == "properties.name").First() as JToken).Values().First()["value"].ToString();
                    parents += parentName + ", ";
                }
                if (!string.IsNullOrWhiteSpace(parents))
                    parents = parents.Substring(0, parents.Length - 2);

                ret.Add(new { me, parents, children });
            }

            return Ok(ret);
        }


        [HttpGet]
        [Route("personById/{id}")]
        public async Task<IHttpActionResult> GetPersonById(string id)
        {
            var person = await getById(id);

            return Ok(new { result = person });
        }

        [HttpGet]
        [Route("idByNameAndParent/{name}/{parent}")]
        public async Task<IHttpActionResult> GetIDByNameAndParent(string name, string parent)
        {
            var grem = new GremlinHelper();
            var persons = await grem.getIdByNameAndParentAsync(name, parent);
            var result = new List<person>();
            foreach (var p in persons)
            {
                result.Add(await getById(p));
            }
            return Ok(new { results = result });
        }

        [HttpGet]
        [Route("BootstrapDB")]
        public async Task<IHttpActionResult> GetBootsrapDB()
        {
            var bs = new BootstrapDB();
            await bs.Bootstrap();
            return Ok();
        }

        public class newChildInfo
        {
            public string name;
            public string gender;
            public string motherId;
            public string fatherId;
            public string motherAge;
            public string fatherAge;
            public string id;
        }
        [HttpPost]
        [Route("AssignChild/{id}")]
        public async Task<IHttpActionResult> PostAssignChild(string id, newChildInfo info)
        {
            var g = new GremlinHelper();

            if (info == null || string.IsNullOrWhiteSpace(info.id) || (string.IsNullOrWhiteSpace(info.motherId) && string.IsNullOrWhiteSpace(info.fatherId)))
                return BadRequest();

            var ncID = info.id;

            // relate to parent                    
            await g.getResultAsync($"g.V('{id}').addE('parent').to(g.V('{ncID}'))");

            // now other parent
            if (!string.IsNullOrWhiteSpace(info.motherId) && info.motherId != id)
                await g.getResultAsync($"g.V('{info.motherId}').addE('parent').to(g.V('{ncID}'))");
            else if (!string.IsNullOrWhiteSpace(info.fatherId) && info.fatherId != id)
                await g.getResultAsync($"g.V('{info.fatherId}').addE('parent').to(g.V('{ncID}'))");

            // add parent Edge type properties
            if (!string.IsNullOrWhiteSpace(info.fatherId))
                await g.getResultAsync($"g.V('{info.fatherId}').outE('parent').property('type', 'Father')");
            if (!string.IsNullOrWhiteSpace(info.motherId))
                await g.getResultAsync($"g.V('{info.motherId}').outE('parent').property('type', 'Mother')");

            // save age on Edge
            if (!string.IsNullOrWhiteSpace(info.fatherAge))
                await g.getResultAsync($"g.V('{info.fatherId}').outE('parent').inV('{ncID}').property('age', '{info.fatherAge}')");
            if (!string.IsNullOrWhiteSpace(info.motherAge))
                await g.getResultAsync($"g.V('{info.motherId}').outE('parent').inV('{ncID}').property('age', '{info.motherAge}')");

            return Ok();
        }

        [HttpPost]
        [Route("NewChild/{id}")]
        public async Task<IHttpActionResult> PostNewChild(string id, newChildInfo info)
        {
            var g = new GremlinHelper();
            
            if (info == null)
                return BadRequest();

            string ncID;

            if (!string.IsNullOrWhiteSpace(info.name) && info.id == null)
            {
                // do the insert
                var qry = $"g.addV('person').property('name', '{info.name}')";
                if (!string.IsNullOrWhiteSpace(info.gender))
                    qry += $".property('gender', '{info.gender}')";
                var nc = await g.getResultAsync(qry);

                // get iD frmo result
                ncID = (nc as JObject)["id"].ToString();
            }
            else
                ncID = info.id;

            // relate to parent                    
            await g.getResultAsync($"g.V('{id}').addE('parent').to(g.V('{ncID}'))");

            // now other parent
            if (!string.IsNullOrWhiteSpace(info.motherId) && info.motherId != id)
                await g.getResultAsync($"g.V('{info.motherId}').addE('parent').to(g.V('{ncID}'))");
            else if (!string.IsNullOrWhiteSpace(info.fatherId) && info.fatherId != id)
                await g.getResultAsync($"g.V('{info.fatherId}').addE('parent').to(g.V('{ncID}'))");

            // add parent Edge type properties
            if (!string.IsNullOrWhiteSpace(info.fatherId))
                await g.getResultAsync($"g.V('{info.fatherId}').outE('parent').property('type', 'Father')");
            if (!string.IsNullOrWhiteSpace(info.motherId))
                await g.getResultAsync($"g.V('{info.motherId}').outE('parent').property('type', 'Mother')");

            // save age on Edge
            if (!string.IsNullOrWhiteSpace(info.fatherAge))
                await g.getResultAsync($"g.V('{ncID}').inE('parent').has('type', 'Father').property('age', '{info.fatherAge}')");
            if (!string.IsNullOrWhiteSpace(info.motherAge))
                await g.getResultAsync($"g.V('{ncID}').inE('parent').has('type', 'Mother').property('age', '{info.motherAge}')");
                
            return Ok();
        }

        public class newSpouseInfo
        {
            public string id;
            public string name;
            public string age;
            public string gender;
            public bool children = false;
        }
        
        [HttpPost]
        [Route("NewSpouse/{id}")]
        public async Task<IHttpActionResult> PostNewSpouse(string id, newSpouseInfo info)
        {
            var g = new GremlinHelper();

            if (info == null || (string.IsNullOrWhiteSpace(info.name) && string.IsNullOrWhiteSpace(info.id)))
                return BadRequest();

            var spID = "";

            if (string.IsNullOrWhiteSpace(info.id))
            {
                // do the insert
                var qry = $"g.addV('person').property('name', '{info.name.Replace("'","''")}')";
                if (!string.IsNullOrWhiteSpace(info.gender))
                    qry += $".property('gender', '{info.gender}')";
                var nc = await g.getResultAsync(qry);

                // get iD from result
                spID = (nc as JObject)["id"].ToString();
            }
            else
                spID = info.id;


            // relate to parent                    
            await g.getResultAsync($"g.V('{id}').addE('married').to(g.V('{spID}'))");           

            if (info.children)
            {

            }

            return Ok();
        }

        [HttpPost]
        [Route("NewProperty/{id}")]
        public async Task<IHttpActionResult> PostNewProperty(string id, keyValue info)
        {
            var g = new GremlinHelper();
            string qry = $"g.V('{id}').property('{info.key.Replace("'", "''")}','{info.value.Replace("'", "''")}')";
            await g.getResultAsync(qry);

            return Ok();
        }

        public class newAreaInfo
        {
            public string area;
            public string country;
        }
        [HttpPost]
        [Route("NewArea/{id}")]
        public async Task<IHttpActionResult> PostNewArea(string id, newAreaInfo info)
        {
            var g = new GremlinHelper();
            string areaId, q;
            if (string.IsNullOrWhiteSpace(info.country))
            {
                q = $"g.V().has('area', 'name', '{info.area}')";
                var area = await g.getResultAsync(q);
                if (area == null)
                {
                    q = $"g.addV('area').property('name', '{info.area}')";
                    areaId = (await g.getResultAsync(q))["id"].ToString();
                }
                else
                {
                    areaId = area["id"].ToString();
                }
            }
            else
            {
                var res = await g.createVertexEdgeVertextByName("area", info.area, "isIn", "country", info.country);
                areaId = res.outId;
            }

            q = $"g.V'{id}').outE('lived').inV().has('area', 'id' '{areaId}')";
            var isAlready = await g.getResultAsync(q);
            if (isAlready == null)
            {
                q = $"g.V('{id}').addE('lived').to(g.V('{areaId}'))";
                await g.getResultAsync(q);
            }
            return Ok();
        }

        public class AreaInfo
        {
            public string id;
            public string name;
            public List<person> residence = new List<person>();
        }

        [HttpGet]
        [Route("AreaInfo/{id}")]
        public async Task<IHttpActionResult> GetAreaInfo(string id)
        {
            var g = new GremlinHelper();

            string name;

            var q = $"g.V().has('area', 'id', '{id}')";
            var info = await g.getResultAsync(q);
            var n = info["properties"].Where(x => x.Path == "properties.name").FirstOrDefault();
            if (n != null)
                name = n.Values()["value"].First().ToString();

            q = $"g.V().has('area', 'id', '{id}').inE().outV()";
            var infoes = await g.getResultsAsync(q);

            foreach (var i in info)
            {
                
            }

            return Ok();
        }
    }
}