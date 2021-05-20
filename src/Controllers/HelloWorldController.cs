using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace FanoutAPIV1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HelloWorldController : ControllerBase
    {
        private readonly string _fanoutHelperAPIHost = Environment.GetEnvironmentVariable("FANOUT-HELPER-API-HOST");

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
            var stopwatch = new Stopwatch();

            stopwatch.Start();

            var timeoutCancellationTokenSource = new CancellationTokenSource(slaMs);

            var timeoutCancellationToken = timeoutCancellationTokenSource.Token;

            var combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationToken);

            var combinedCancellationToken = combinedCancellationTokenSource.Token;

            var tasksPerParentTask = tasksPerRequest / 4;

            var parentTasks = new List<Task<FanoutTaskStatus>>
            {
                ParentTask(
                    tasksPerParentTask,
                    taskDelayMs,
                    combinedCancellationToken),

                ParentTask(
                    tasksPerParentTask,
                    taskDelayMs,
                    combinedCancellationToken),

                ParentTask(
                    tasksPerParentTask,
                    taskDelayMs,
                    combinedCancellationToken),

                ParentTask(
                    tasksPerParentTask,
                    taskDelayMs,
                    combinedCancellationToken),

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

        private async Task<FanoutTaskStatus> ParentTask(
            int numberOfChildTasks,
            int taskDelayMs,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            for (byte i = 0; i < numberOfChildTasks; i++)
            {
                tasks.Add(ChildTask(
                    taskDelayMs,
                    cancellationToken));
            }

            await Task.WhenAll(tasks);

            int successfulTasks = tasks.Count(x => x.IsCompletedSuccessfully);

            return new FanoutTaskStatus(
                successfulTasks,
                numberOfChildTasks - successfulTasks);
        }

        private async Task ChildTask(
            int childTaskDelayMs,
            CancellationToken cancellationToken)
        {
            await Task.Delay(
                childTaskDelayMs,
                cancellationToken);
        }
    }

    public record FanoutTaskStatus(
        int NumberOfSuccessfulTasks,
        int NumberOfFailedTasks);

    public record Result(
        long ServerProcessingTimeMs,
        FanoutTaskStatus CombinedTaskStatus);
}