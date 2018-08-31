using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Graphs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GraphNet.Controllers
{
    public class GremlinHelper
    {
        static string endpoint = "https://liebmos.documents.azure.com:443";
        static string authKey = "REPLACE";
        static DocumentClient client = new DocumentClient(
                new Uri(endpoint),
                authKey,
                new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp });

        static DocumentCollection graph = null;

        public async Task<DocumentCollection> GetGraph()
        {
            await initGraph();
            return graph;
        }

        private async Task<DocumentCollection> initGraph()
        {
            if (graph == null)
            {
                graph = await client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri("graphdb"),
                new DocumentCollection { Id = "Persons2" },
                new RequestOptions { OfferThroughput = 400 });
            }
            return graph;
        }
        
        public class createVertexEdgeVertextByNameResult
        {
            public string outId;
            public string inId;
        }
        /// <summary>
        /// Out vertext -> Edge -> In Vertex
        /// </summary>
        /// <param name="outLabel"></param>
        /// <param name="outName"></param>
        /// <param name="edgeLabel"></param>
        /// <param name="inLabel"></param>
        /// <param name="inName"></param>
        /// <returns></returns>
        public async Task<createVertexEdgeVertextByNameResult> createVertexEdgeVertextByName(string outLabel, string outName, string edgeLabel, string inLabel, string inName)
        {
            outName = outName.Replace("'", "''");
            string parentId, childId;

            string p = $"g.V('{outLabel}').has('{outLabel}', 'name', '{outName}').outE('{edgeLabel}').inV().has('{inLabel}', 'name', '{inName}')'";
            var result = await getResultAsync(p);
            if (result != null)
            {
                childId = result["id"].ToString();
                p = $"g.V('{childId}').inE('{edgeLabel}').outV('{outLabel}').has('{outLabel}', 'name', '{outName}')";
                result = await getResultAsync(p);
                parentId = result["id"].ToString();

                return new createVertexEdgeVertextByNameResult() {  outId = parentId, inId = childId};
            }
                
            
            p = $"g.V('{outLabel}').has('{outLabel}', 'name', '{outName}')";
            result = await getResultAsync(p);
            if (result == null)
            {
                p = $"p.addEdge('{outLabel}').properties('name', '{outName}')";
                parentId = (await getResultAsync(p))["id"].ToString();
            } else
                parentId = result["id"].ToString();

            p = $"g.V('{inLabel}').has('{inLabel}', 'name', '{inName}')";
            result = await getResultAsync(p);
            if (result == null)
            {
                p = $"p.addEdge('{inLabel}').properties('name', '{inName}')";
                childId = (await getResultAsync(p))["id"].ToString();
            }
            else
                childId = result["id"].ToString();

            await getResultAsync($"g.V('{parentId}').addE('{edgeLabel}').to(g.V('{childId}'))");

            return new createVertexEdgeVertextByNameResult() { outId = parentId, inId = childId };
        }

        public async Task<List<string>> getIdsByNameAsync(string name)
        {
            var result = await getResultsAsync($"g.V().has('person', 'name', '{name}')");
            return result.Select(x => (x as JObject)["id"].ToString()).ToList();
        }

        
        public async Task<List<string>> getIdByNameAndParentAsync(string name, string parent)
        {
            var result = await getResultsAsync($"g.V().has('person', 'name', '{name}').as('o').inE('parent').outV().has('person', 'name', '{parent}').sel");
            return result.Select(x => (x as JObject)["id"].ToString()).ToList();            
        }

        public async Task<FeedResponse<object>> getPassthroughResult(string gremlin)
        {
            //await initGraph();

            // var query = client.CreateGremlinQuery<dynamic>(graph, gremlin);
            // return (await query.ExecuteNextAsync() as FeedResponse<object>);            

            var th = new TinkerHelper();
            var qryResult = await th.ProcessCommand(gremlin);
            return JsonConvert.DeserializeObject<FeedResponse<object>>(qryResult);
        }

        // query that expects only 1 result -- return null if 0 or 2+ results in resultset
        public async Task<JObject> getResultAsync(string gremlin)
        {
            // await initGraph();
            var th = new TinkerHelper();

            // var query = client.CreateGremlinQuery<dynamic>(graph, gremlin);
            // var result = (await query.ExecuteNextAsync() as FeedResponse<object>);
            var qryResult = await th.ProcessCommand(gremlin);
            var result = JsonConvert.DeserializeObject<FeedResponse<object>>(qryResult);
            if (result.Count() == 1)
                return result.First() as JObject;
            else
                return null;
        }

        public async Task<List<dynamic>> getResultsAsync(string gremlin)
        {
            List<dynamic> retValue = new List<dynamic>();

            // await initGraph();
            // var query = client.CreateGremlinQuery<dynamic>(graph, gremlin);

            //while (query.HasMoreResults)
            //{
            //    foreach (dynamic result in await query.ExecuteNextAsync())
            //    {
            //        retValue.Add(result);
            //    }
            //}
            
            var th = new TinkerHelper();
            var qryResult = await th.ProcessCommand(gremlin);
            var query = JsonConvert.DeserializeObject<FeedResponse<object>>(qryResult);
            foreach (var res in query)
            {
                retValue.Add(res);
            }

            return retValue;
        }
    }
}
