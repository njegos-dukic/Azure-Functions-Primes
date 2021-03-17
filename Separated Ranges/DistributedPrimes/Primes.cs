using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Numerics;
using System.Diagnostics;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace DistributedPrimes
{
    public static class Primes
    {
        private static ulong scaleFactor = 10;
        private static ulong recursionLimit = 5000;
        private static ulong totalPrimes = 0;
        private static ulong maxDepth = (ulong) Environment.ProcessorCount;
        private static bool control = false;

        [FunctionName("PrimesHttpStart")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            totalPrimes = 0;
            control = true;

            string requestBody = await req.Content.ReadAsStringAsync(); // HttpContext.Request.ReadAsStringAsync(); // new StreamReader(req.Body).ReadToEndAsync();
            string id = await starter.StartNewAsync("OrchestratePrimes", input: requestBody);
            
            var response = starter.CreateCheckStatusResponse(req, id);

            if (response == null)
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);

            return response;
        }

        [FunctionName("OrchestratePrimes")]
        public static async Task<JArray> RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            dynamic data = context.GetInput<string>();
            dynamic request = JsonConvert.DeserializeObject(data);
            List<SingleCount> counts = new List<SingleCount>();

            foreach (var range in request.ranges)
            {
                string[] endPoints = new string[2];
                endPoints[0] = range.start;
                endPoints[1] = range.end;

                var task = context.CallSubOrchestratorAsync<string[]>("SubOrchestrate", endPoints);
                var result = await Task.WhenAll(task);

                List<string[]> output = new List<string[]>();
                output.AddRange(result);
                List<string> primes = new List<string>();

                foreach (string[] s in output)
                    primes.AddRange(s);

                ulong count = 0;
                string values = "";
                foreach (string s in primes)
                {
                    count++;
                    values += s + " ";
                }

                counts.Add(new SingleCount(endPoints[0], endPoints[1], count, values));
            }

            JArray retArray = new JArray
            {
                JObject.FromObject(new { total = totalPrimes })
            };

            foreach (var count in counts)
            {
                retArray.Add(JObject.FromObject(new
                {
                    range = new
                    {
                        start = count.start,
                        end = count.end,
                        count = count.count,
                        values = count.values.Trim()
                    }
                }));
            }

            control = false;

            return retArray;
        }

        [FunctionName("SubOrchestrate")]
        public static async Task<string[]> GetPrimesFromRange([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log) =>
            await DistributeFunction(context, log, "FindPrimes");

        static async Task<string[]> DistributeFunction(IDurableOrchestrationContext context, ILogger log, string functionName)
        {
            string[] endPoints = context.GetInput<string[]>();
            ulong start = ulong.Parse(endPoints[0]);
            ulong end = ulong.Parse(endPoints[1]);

            List<Task<string[]>> tasks = new List<Task<string[]>>();

            ulong boundary = (end - start) / scaleFactor;

            for (ulong i = 0; i < scaleFactor - 1; i++)
                tasks.Add(context.CallActivityAsync<string[]>(functionName, (start + boundary * i, start + boundary * (i + 1) - 1, maxDepth)));

            tasks.Add(context.CallActivityAsync<string[]>(functionName, (start + boundary * (scaleFactor - 1), end, maxDepth)));

            var result = await Task.WhenAll(tasks);
            List<string[]> output = new List<string[]>();
            output.AddRange(result);
            List<string> primes = new List<string>();

            foreach (string[] s in output)
                if (s != null)
                    primes.AddRange(s);

            totalPrimes += (ulong) primes.Count;
            return primes.ToArray();
        }

        [FunctionName("FindPrimes")]
        public static string[] FindPrimesInRange([ActivityTrigger] (ulong start, ulong end, ulong depth) input, ILogger log) =>
            PrimesLogic(input.start, input.end, input.depth);

        public static string[] PrimesLogic(ulong start, ulong end, ulong depth)
        {
            if (!control || end < start)
                return null;

            List<string> primes = new List<string>();

            if (end - start >= recursionLimit && depth > 0)
            {
                Parallel.Invoke(
                        () => primes.AddRange(PrimesLogic(start, start + ((end - start) / 2), depth - 1)),
                        () => primes.AddRange(PrimesLogic((start + (end - start) / 2 + 1), end, depth - 1)));
            }

            else
            {
                for (ulong i = start; i <= end; i++)
                {
                    bool prime = true;

                    for (ulong j = 2; j < i / 2 + 1; j++)
                        if (i % j == 0)
                        {
                            prime = false;
                            break;
                        }

                    if (prime && i >= 2)
                        primes.Add(i.ToString() + " ");
                }
            }

            return primes.ToArray();
        }
    }

    class SingleCount
    {
        public string start { get; set; }
        public string end { get; set; }
        public ulong count { get; set; }
        public string values { get; set; }

        public SingleCount(string start, string end, ulong count, string values)
        {
            this.start = start;
            this.end = end;
            this.count = count;
            this.values = values;
        }
    }
}