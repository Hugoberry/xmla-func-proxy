using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace xmla_func_proxy
{
    class QueryResult : ActionResult
    {
        private AdomdDataReader queryResults;
        private ConnectionPoolEntry con;
        private ConnectionPool pool;
        public QueryResult(AdomdDataReader queryResults, ConnectionPoolEntry con, ConnectionPool pool)
        {
            this.queryResults = queryResults;
            this.con = con;
            this.pool = pool;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {

            context.HttpContext.Response.ContentType = "application/json";
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.OK;

            var streaming = true;

            await context.HttpContext.Response.StartAsync();

            var responseStream = context.HttpContext.Response.Body;
            System.IO.Stream encodingStream = responseStream;

            try
            {
                if (streaming)
                {
                    await WriteResultsToStream(queryResults, encodingStream);
                }
                else
                {
                    var ms = new MemoryStream();
                    await WriteResultsToStream(queryResults, ms);
                    ms.Position = 0;
                    var buf = new byte[256];
                    ms.Read(buf, 0, buf.Length);
                    var str = System.Text.Encoding.UTF8.GetString(buf);
                    ms.Position = 0;


                    await ms.CopyToAsync(encodingStream);
                }

                pool.ReturnConnection(con);
                await encodingStream.FlushAsync();
                await responseStream.FlushAsync();
                await context.HttpContext.Response.CompleteAsync();

            }
            catch (Exception ex)
            {
                con.Connection.Dispose(); //do not return to pool
                throw;  //too late to send error to client  
            }

        }

        static async Task WriteResultsToStream(AdomdDataReader results, Stream stream)
        {

            if (results == null)
            {
                //log.LogInformation("Null results");
                return;
            }

            using var rdr = results;

            //can't call Dispose on these without syncronous IO on the underlying connection
            var tw = new StreamWriter(stream, null, 1024 * 4, true);
            var w = new Newtonsoft.Json.JsonTextWriter(tw);
            int rows = 0;

            try
            {
                await w.WriteStartArrayAsync(cancel);

                while (rdr.Read())
                {
                    if (cancel.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }
                    rows++;
                    await w.WriteStartObjectAsync(cancel);
                    for (int i = 0; i < rdr.FieldCount; i++)
                    {
                        string name = rdr.GetName(i);
                        object value = rdr.GetValue(i);

                        await w.WritePropertyNameAsync(name, cancel);
                        await w.WriteValueAsync(value, cancel);
                    }
                    await w.WriteEndObjectAsync(cancel);

                    if (rows % 50000 == 0)
                    {
                        log.LogInformation($"Wrote {rows} rows to output stream.");
                    }
                }
                log.LogInformation($"Finished Writing {rows} rows to output stream.");

                await w.WriteEndArrayAsync(cancel);

                await w.FlushAsync();
                await tw.FlushAsync();
                await stream.FlushAsync();

            }
            catch (TaskCanceledException ex)
            {
                log.LogWarning($"Writing results canceled after {rows} rows.");
            }

        }
    }
}