using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DistributedPrimes
{
    public static class Primes
    {
        public static int scaleFactor = 10;
        public static readonly ulong recursionLimit = 1000;
        public static readonly ulong maxDepth = (ulong) Environment.ProcessorCount;
        public static bool control = false;
        public static List<Computation> computations = new List<Computation>();
        public static List<Computation> toDistribute = new List<Computation>();
        public static List<Computation>[] arguments = null;
        public static List<List<Computation>> toCombine = new List<List<Computation>>();

        [FunctionName("PrimesHttpStart")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            arguments = null;
            control = true;

            computations?.Clear();
            toDistribute?.Clear();

            toCombine?.Clear();

            Computation.TotalCount = 0;

            string requestBody = await req.Content.ReadAsStringAsync();
            string id = await starter.StartNewAsync("OrchestratePrimes", input: requestBody);
            
            var response = starter.CreateCheckStatusResponse(req, id);

            if (response == null)
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);

            return response;
        }

        [FunctionName("OrchestratePrimes")]
        public static async Task<string> RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            dynamic data = context.GetInput<string>();
            dynamic request = JsonConvert.DeserializeObject(data);

            scaleFactor = int.Parse((string)request.scaleFactor);
            arguments = new List<Computation>[scaleFactor];

            foreach (var range in request.ranges)
            {
                ulong start = ulong.Parse((string)range.start);
                ulong end = ulong.Parse((string)range.end);
                computations.Add(new Computation(start, end, true));
            }

            ulong scaleLength = (ulong)Math.Ceiling((double)Computation.TotalCount / (double)scaleFactor);

            List<Computation> shorts = new List<Computation>();
            ulong shortsCount = 0;

            foreach (var computation in computations)
            {
                if (computation.Range > scaleLength)
                    toDistribute.AddRange(computation.SplitToFit(scaleLength));

                else if (shortsCount + computation.Range <= scaleLength)
                {
                    shorts.Add(computation);
                    shortsCount += computation.Range;
                }
                else
                {
                    toCombine.Add(shorts);

                    shorts = new List<Computation>();
                    shorts.Add(computation);
                    shortsCount = computation.Range;
                }
            }

            toCombine.Add(shorts);

            for (int i = 0; i < scaleFactor; i++)
                arguments[i] = new List<Computation>();

            int k = 0;

            foreach (var arg in toDistribute)
            {
                arguments[k % scaleFactor].Add(arg);
                k++;
            }

            foreach (var s in toCombine)
            {
                arguments[k % scaleFactor].AddRange(s);
                k++;
            }

            var task = context.CallSubOrchestratorAsync<string>("SubOrchestrate", null);

            return await task;
        }

        [FunctionName("SubOrchestrate")]
        public static async Task<string> GetPrimesFromRange([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log) =>
            await DistributeFunction(context, log, "FindPrimes");

        static async Task<string> DistributeFunction(IDurableOrchestrationContext context, ILogger log, string functionName)
        {
            List<Task<int>> tasks = new List<Task<int>>();

            for (int i = 0; i < scaleFactor; i++)
                tasks.Add(context.CallActivityAsync<int>(functionName, (arguments[i], maxDepth)));

            var output = await Task.WhenAll(tasks);

            ulong count = 0;

            foreach (int x in output)
                count += (ulong) x;

            return "Count: " + count.ToString();
        }

        [FunctionName("FindPrimes")]
        public static ulong FindPrimesInRange([ActivityTrigger] (List<Computation> ranges, ulong depth) input, ILogger log) =>
            PrimesLogic(input.ranges, input.depth);

        public static ulong PrimesLogic(List<Computation> range, ulong depth)
        {
            object locker = new object();
            ulong counter = 0;
            ulong localCounter1 = 0;
            ulong localCounter2 = 0;

            foreach (var computation in range)
            {
                if (computation.Range >= recursionLimit && depth > 0)
                {
                    localCounter1 = 0;
                    localCounter2 = 0;
                    
                    Parallel.Invoke
                    (
                         () => localCounter1 += PrimesLogic(new List<Computation> { new Computation(computation.Start, computation.Start + (computation.End - computation.Start) / 2 - 1, false) }, depth - 1),
                         () => localCounter2 += PrimesLogic(new List<Computation> { new Computation(computation.Start + (computation.End - computation.Start) / 2, computation.End, false) }, depth - 1)
                    );

                    lock(locker)
                        counter += localCounter1 + localCounter2;
                }

                else
                {
                    for (ulong i = computation.Start; i <= computation.End; i++)
                    {
                        bool prime = true;

                        for (ulong j = 2; j < i / 2 + 1; j++)
                            if (i % j == 0)
                            {
                                prime = false;
                                break;
                            }

                        if (prime && i >= 2)
                            counter++;
                    }
                }
            }

            return counter;
        }
    }

    public class Computation
    {
        public static ulong TotalCount = 0;

        public ulong Start { get; set; }

        public ulong End { get; set; }

        public ulong Range { get; set; }

        public Computation(ulong start, ulong end, bool initial)
        {
            if (start > end)
            {
                this.Start = end;
                this.End = start;
            }
            else
            {
                this.Start = start;
                this.End = end;
            }

            this.Range = this.End - this.Start;

            if (initial)
                TotalCount += this.Range;
        }

        public LinkedList<Computation> SplitToFit(ulong averageRange)
        { 
            LinkedList<Computation> list = new LinkedList<Computation>();

            ulong rangesCount = (ulong) Math.Ceiling((double) this.Range / (double) averageRange);
            for (ulong i = 0; i < rangesCount; i++)
            {
                if (i < rangesCount - 1)
                    list.AddLast(new Computation(Start + averageRange * i, Start + averageRange * (i + 1) - 1, false)); 
                else
                    list.AddLast(new Computation(Start + averageRange * i, End, false));
            }

            return list;
        }
    }
}