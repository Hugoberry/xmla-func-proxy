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

namespace Xmla.Func.Proxy
{
    public static class Query
    {
        private static ConnectionPool pool = new ConnectionPool();
        private static readonly string constr = Environment.GetEnvironmentVariable("DatasetConnectionString");

        [FunctionName("Query")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Starting lambda");
            string query = await new StreamReader(req.Body).ReadToEndAsync();
            
            log.LogInformation($"Running the following DAX >>> {query}");
            return await GetQueryResult(query, log);
        }

        private static async Task<IActionResult> GetQueryResult(string query, ILogger log)
        {

            ConnectionPoolEntry con;
            try
            {

                con = pool.GetConnection(constr);
                var cmd = con.Connection.CreateCommand();

                cmd.CommandText = query;
                cmd.CommandTimeout = 2 * 60;

                object queryResults;
                log.LogInformation("Start  DAX execution");
                queryResults = cmd.Execute();
                log.LogInformation("Finish DAX execution");


                if (queryResults is AdomdDataReader rdr)
                {
                    return new QueryResult(rdr,true,false,con,pool,log);
                }

                return new BadRequestResult();

            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                log.LogError(msg);
                return new BadRequestObjectResult(msg);
            }
        }


    }
}
