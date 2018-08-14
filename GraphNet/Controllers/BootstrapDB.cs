using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Graphs;
using Newtonsoft.Json.Linq;

namespace GraphNet.Controllers
{
    public class BootstrapDB
    {       
        public async Task Bootstrap()
        { 
            string adam;
            string eve;
            string cain;
            string seth;
            string abel;
            string enosh;
            string kenan;

            var g = new GremlinHelper();

            List<string> initialQueries = new List<string>
            {
                { "g.V().drop()" },
                { "g.addV('person').property('name', 'Adam').property('gender', 'M')" },
                { "g.addV('person').property('name', 'Eve').property('gender', 'F')" },
                { "g.addV('person').property('name', 'Cain').property('gender', 'M')" },
                { "g.addV('person').property('name', 'Abel').property('gender', 'M')" },
                { "g.addV('person').property('name', 'Seth').property('gender', 'M')" },
                { "g.addV('person').property('name', 'Enosh').property('gender', 'M')" },
                { "g.addV('person').property('name', 'Kenan').property('gender', 'M')" }
            };

            foreach (var q in initialQueries)
            {
                await g.getResultAsync(q);
            }

            adam = (await g.getIdsByNameAsync("Adam"))[0];
            eve = (await g.getIdsByNameAsync("Eve"))[0];
            cain = (await g.getIdsByNameAsync("Cain"))[0];
            seth = (await g.getIdsByNameAsync("Seth"))[0];
            abel = (await g.getIdsByNameAsync("Abel"))[0];
            enosh = (await g.getIdsByNameAsync("Enosh"))[0];
            kenan = (await g.getIdsByNameAsync("Kenan"))[0];

            await g.getResultAsync($"g.V('{adam}').addE('married').to(g.V('{eve}'))");
            
            await g.getResultAsync($"g.V('{eve}').addE('parent').to(g.V('{cain}'))");
            await g.getResultAsync($"g.V('{eve}').addE('parent').to(g.V('{abel}'))");
            await g.getResultAsync($"g.V('{eve}').addE('parent').to(g.V('{seth}'))");

            await g.getResultAsync($"g.V('{adam}').addE('parent').to(g.V('{cain}'))");
            await g.getResultAsync($"g.V('{adam}').addE('parent').to(g.V('{abel}'))");            
            await g.getResultAsync($"g.V('{adam}').addE('parent').to(g.V('{seth}'))");
            
            // only 1 child, so can update entire path for seth
            await g.getResultAsync($"g.V('{seth}').addE('parent').to(g.V('{enosh}'))");
            await g.getResultAsync($"g.V('{seth}').outE('parent').property('type', 'Father').property('age', 105)");

            await g.getResultAsync($"g.V('{enosh}').addE('parent').to(g.V('{kenan}'))");
            await g.getResultAsync($"g.V('{kenan}').inE('parent').has('type', 'Father').property('age', 90)");

            // where as multiple children, and we want to update only  Seth -> parent.fathe
            await g.getResultAsync($"g.V('{seth}').inE('parent').has('type', 'Father').property('age', 130)");
            // Adam -> parent -> Seth path            
            // await g.getResultAsync($"g.V('{adam}').outE('parent').inV().has('person', 'name', 'Seth').as('s').inE().has('type', 'Father').property('age', 130)");
            await g.getResultAsync($"g.V('{eve}').outE('parent').property('type', 'Mother')");
            await g.getResultAsync($"g.V('{adam}').outE('parent').property('type', 'Father')");

            
        }
    }
}