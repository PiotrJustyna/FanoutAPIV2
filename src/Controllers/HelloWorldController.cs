using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class HelloWorldController : ControllerBase
{
    private const int numberOfChildTasks = 250;
    private const int delayMs = 1000;

    [HttpGet]
    public async Task<ActionResult<Result>> Get(
        int slaMs,
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

        var parentTasks = new List<Task<FanoutTaskStatus>>
        {
            ParentTask(combinedCancellationToken),
            ParentTask(combinedCancellationToken),
            ParentTask(combinedCancellationToken),
            ParentTask(combinedCancellationToken)
        };
        
        var workTask = Task.WhenAll(parentTasks);

        await Task.WhenAny(
            workTask,
            Task.Delay(slaMs, combinedCancellationToken));

        FanoutTaskStatus result = parentTasks
            .Select(x =>
                x.IsCompletedSuccessfully
                    ? new FanoutTaskStatus(x.Result.NumberOfSuccessfulTasks, x.Result.NumberOfFailedTasks)
                    : new FanoutTaskStatus(0, numberOfChildTasks))
            .Aggregate((x, y) =>
                new FanoutTaskStatus(
                    x.NumberOfSuccessfulTasks + y.NumberOfSuccessfulTasks,
                    x.NumberOfFailedTasks + y.NumberOfFailedTasks));

        stopwatch.Stop();

        if (result.NumberOfFailedTasks > 0)
        {
            return NoContent();
        }
        else
        {
            return Ok(new Result(
                stopwatch.ElapsedMilliseconds,
                result));
        }
    }

    private async Task<FanoutTaskStatus> ParentTask(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        for (byte i = 0; i < numberOfChildTasks; i++)
        {
            tasks.Add(ChildTask(cancellationToken));
        }

        await Task.WhenAll(tasks);

        int successfulTasks = tasks.Count(x => x.IsCompletedSuccessfully);

        return new FanoutTaskStatus(
            successfulTasks,
            numberOfChildTasks - successfulTasks);
    }

    private async Task ChildTask(CancellationToken cancellationToken)
    {
        await Task.Delay(
            delayMs,
            cancellationToken);
    }
}

public record FanoutTaskStatus(
    int NumberOfSuccessfulTasks,
    int NumberOfFailedTasks);
    
public record Result(
    long ServerProcessingTimeMs,
    FanoutTaskStatus CombinedTaskStatus);