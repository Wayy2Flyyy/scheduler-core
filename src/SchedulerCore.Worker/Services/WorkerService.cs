using System.Net.Http.Json;
using SchedulerCore.Domain.Entities;
using SchedulerCore.Worker.Handlers;

namespace SchedulerCore.Worker.Services;

public class WorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _workerId;
    private readonly string _workerName;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _leaseDuration;

    public WorkerService(
        IServiceProvider serviceProvider,
        ILogger<WorkerService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("Coordinator");
        
        _workerId = Guid.NewGuid().ToString();
        _workerName = Environment.MachineName + "_" + _workerId[..8];
        _pollInterval = TimeSpan.FromSeconds(_configuration.GetValue<int>("Worker:PollIntervalSeconds", 10));
        _heartbeatInterval = TimeSpan.FromSeconds(_configuration.GetValue<int>("Worker:HeartbeatIntervalSeconds", 30));
        _leaseDuration = TimeSpan.FromMinutes(_configuration.GetValue<int>("Worker:LeaseDurationMinutes", 5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerName} ({WorkerId}) starting", _workerName, _workerId);

        // Start heartbeat task
        var heartbeatTask = Task.Run(() => SendHeartbeatsAsync(stoppingToken), stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing jobs");
                }

                await Task.Delay(_pollInterval, stoppingToken);
            }
        }
        finally
        {
            await heartbeatTask;
            _logger.LogInformation("Worker {WorkerName} stopped", _workerName);
        }
    }

    private async Task SendHeartbeatsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // In a real implementation, this would call the coordinator API
                _logger.LogDebug("Worker {WorkerName} sending heartbeat", _workerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat");
            }

            await Task.Delay(_heartbeatInterval, cancellationToken);
        }
    }

    private async Task ProcessJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        // In a real implementation, this would:
        // 1. Call coordinator API to acquire a job
        // 2. Get the appropriate handler for the job type
        // 3. Execute the job
        // 4. Report results back to coordinator
        
        var handlers = scope.ServiceProvider.GetServices<IJobHandler>().ToList();
        _logger.LogDebug("Worker {WorkerName} has {HandlerCount} registered handlers", 
            _workerName, handlers.Count);
        
        // Simulate checking for jobs (placeholder for actual API call)
        await Task.CompletedTask;
    }

    private async Task<bool> ExecuteJobAsync(Job job, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IJobHandler>();
        var handler = handlers.FirstOrDefault(h => h.JobType == job.Type);

        if (handler == null)
        {
            _logger.LogError("No handler found for job type {JobType}", job.Type);
            return false;
        }

        try
        {
            _logger.LogInformation("Executing job {JobId} of type {JobType}", job.Id, job.Type);
            var result = await handler.ExecuteAsync(job, cancellationToken);
            _logger.LogInformation("Job {JobId} completed with result: {Result}", job.Id, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing job {JobId}", job.Id);
            return false;
        }
    }
}
