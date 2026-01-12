using SchedulerCore.Domain.Entities;

namespace SchedulerCore.Worker.Handlers;

/// <summary>
/// Base interface for job handlers
/// </summary>
public interface IJobHandler
{
    string JobType { get; }
    Task<bool> ExecuteAsync(Job job, CancellationToken cancellationToken);
}

/// <summary>
/// Sample job handler for demonstration
/// </summary>
public class SampleJobHandler : IJobHandler
{
    private readonly ILogger<SampleJobHandler> _logger;

    public string JobType => "sample";

    public SampleJobHandler(ILogger<SampleJobHandler> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(Job job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing sample job {JobId} with payload: {Payload}", 
            job.Id, job.Payload);

        // Simulate work
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        _logger.LogInformation("Completed sample job {JobId}", job.Id);
        return true;
    }
}

/// <summary>
/// Echo job handler that logs the payload
/// </summary>
public class EchoJobHandler : IJobHandler
{
    private readonly ILogger<EchoJobHandler> _logger;

    public string JobType => "echo";

    public EchoJobHandler(ILogger<EchoJobHandler> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(Job job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Echo job {JobId}: {Payload}", job.Id, job.Payload);
        await Task.CompletedTask;
        return true;
    }
}
