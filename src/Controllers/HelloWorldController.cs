using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Interfaces;
using Microsoft.AspNetCore.Mvc;
using Orleans;

namespace FanoutAPIV1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HelloWorldController : ControllerBase
    {
        private readonly IClusterClient _clusterClient;

        private readonly Random _generator;
        
        private readonly string _fanoutHelperAPIHost = Environment.GetEnvironmentVariable("FANOUT-HELPER-API-HOST");
        
        public HelloWorldController(IClusterClient clusterClient)
        {
            _clusterClient = clusterClient;
            _generator = new Random();
        }

        private readonly IHttpClientFactory _clientFactory;

        public HelloWorldController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [HttpGet]
        public async Task<ActionResult<Result>> Get(
            int slaMs,
            int tasksPerRequest,
            int taskDelayMs,
            CancellationToken cancellationToken)
        {
            var grain1 = _clusterClient.GetGrain<IWorker>(_generator.Next(0, Int32.MaxValue));
            var grain2 = _clusterClient.GetGrain<IWorker>(_generator.Next(0, Int32.MaxValue));
            var grain3 = _clusterClient.GetGrain<IWorker>(_generator.Next(0, Int32.MaxValue));
            var grain4 = _clusterClient.GetGrain<IWorker>(_generator.Next(0, Int32.MaxValue));

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var timeoutCancellationTokenSource = new CancellationTokenSource(slaMs);

            var timeoutCancellationToken = timeoutCancellationTokenSource.Token;

            var combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationToken);

            var combinedCancellationToken = combinedCancellationTokenSource.Token;

            // 2021-05-31 PJ:
            // Link to the combinedCancellationToken.
            // Also, reduce grain disposal time on the backend side.
            var grainCancellationTokenSource = new GrainCancellationTokenSource();

            var tasksPerParentTask = tasksPerRequest / 4;

            var parentTasks = new List<Task<FanoutTaskStatus>>
            {
                grain1.ParentTask(
                    tasksPerParentTask,
                    taskDelayMs,
                    grainCancellationTokenSource.Token),

                grain2.ParentTask(
                    tasksPerParentTask,
                    taskDelayMs,
                    grainCancellationTokenSource.Token),

                grain3.ParentTask(
                    tasksPerParentTask,
                    taskDelayMs,
                    grainCancellationTokenSource.Token),

                grain4.ParentTask(
                    tasksPerParentTask,
                    taskDelayMs,
                    grainCancellationTokenSource.Token),
                
                APICall(
                    slaMs,
                    tasksPerRequest,
                    taskDelayMs,
                    cancellationToken)
            };

            var workTask = Task.WhenAll(parentTasks);

            await Task.WhenAny(
                workTask,
                Task.Delay(slaMs, combinedCancellationToken));

            FanoutTaskStatus result = parentTasks
                .Select(x =>
                    x.IsCompletedSuccessfully
                        ? new FanoutTaskStatus(x.Result.NumberOfSuccessfulTasks, x.Result.NumberOfFailedTasks)
                        : new FanoutTaskStatus(0, tasksPerParentTask))
                .Aggregate((x, y) =>
                    new FanoutTaskStatus(
                        x.NumberOfSuccessfulTasks + y.NumberOfSuccessfulTasks,
                        x.NumberOfFailedTasks + y.NumberOfFailedTasks));

            stopwatch.Stop();

            if (result.NumberOfFailedTasks > 0)
            {
                return NoContent();
            }

            return Ok(new Result(
                stopwatch.ElapsedMilliseconds,
                result));
        }

        private async Task<FanoutTaskStatus> APICall(
            int slaMs,
            int tasksPerRequest,
            int taskDelayMs,
            CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"http://{_fanoutHelperAPIHost}/helper?slaMs={slaMs}&tasksPerRequest={tasksPerRequest}&taskDelayMs={taskDelayMs}");

            var client = _clientFactory.CreateClient();

            var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<Result>(
                null,
                cancellationToken);

            return result.CombinedTaskStatus;
        }
    }

    public record Result(
        long ServerProcessingTimeMs,
        FanoutTaskStatus CombinedTaskStatus);
}