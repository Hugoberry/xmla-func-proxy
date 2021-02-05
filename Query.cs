using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AnalysisServices.AdomdClient;
using Newtonsoft.Json;
using System;

namespace xmla_func_proxy
{
    public static class Query
    {
        private static ConnectionPool pool;
        private static readonly string constr = Environment.GetEnvironmentVariable("DatasetConnectionString");

        [FunctionName("Query")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string query = await new StreamReader(req.Body).ReadToEndAsync();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);

            string responseMessage = $"Processing the DAX query";

            return await GetQueryResult(query);
            //return new OkObjectResult(responseMessage);
        }

        private static async Task<IActionResult> GetQueryResult(string query)
        {



            //ConnectionPoolEntry con;
            //con = pool.GetConnection(constr);

            var con = new AdomdConnection(constr);

            con.Open();

            var cmd = con.CreateCommand();

            cmd.CommandText = query;
            cmd.CommandTimeout = 2 * 60;
            object queryResults;
            queryResults = cmd.Execute();



            if (queryResults is AdomdDataReader rdr)
            {
                return new OkObjectResult(queryResults);
                //return new QueryResult(rdr, con, pool);
            }

            return new BadRequestResult();
            //return bad result
        }


    }
}
