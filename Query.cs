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
            log.LogInformation("C# HTTP trigger function processed a request.");

            string query = await new StreamReader(req.Body).ReadToEndAsync();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);

            string responseMessage = $"Processing the DAX query";

            return await GetQueryResult(query, log);
            //return new OkObjectResult(responseMessage);
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
                log.LogInformation("Star execution");
                queryResults = cmd.Execute();
                log.LogInformation("Finished execution");


                if (queryResults is AdomdDataReader rdr)
                {
                    //return new OkObjectResult(queryResults);
                    //return new QueryResult(rdr, con, pool, log);
                    var stream = new MemoryStream();
                    var encoding = new System.Text.UTF8Encoding(false);
                    using (var tw = new StreamWriter(stream, encoding, 1024 * 4, true))
                    using (var w = new Newtonsoft.Json.JsonTextWriter(tw))
                    {

                        await w.WriteStartObjectAsync();
                        var rn = "rows";

                        await w.WritePropertyNameAsync(rn);
                        await w.WriteStartArrayAsync();

                        while (rdr.Read())
                        {
                            await w.WriteStartObjectAsync();
                            for (int i = 0; i < rdr.FieldCount; i++)
                            {
                                string name = rdr.GetName(i);
                                object value = rdr.GetValue(i);

                                await w.WritePropertyNameAsync(name);
                                await w.WriteValueAsync(value);
                            }
                            await w.WriteEndObjectAsync();
                        }

                        await w.WriteEndArrayAsync();
                        await w.WriteEndObjectAsync();

                        await w.FlushAsync();
                        await tw.FlushAsync();
                        await stream.FlushAsync();

                        stream.Seek(0, SeekOrigin.Begin);
                        return new FileStreamResult(stream, "application/json");
                    }
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
