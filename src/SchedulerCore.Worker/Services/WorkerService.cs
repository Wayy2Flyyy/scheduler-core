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
    private Guid _workerId;
    private readonly string _workerName;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _heartbeatInterval;
    private readonly int _leaseDurationMinutes;

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
        
        var tempId = Guid.NewGuid();
        _workerName = Environment.MachineName + "_" + tempId.ToString()[..8];
        _pollInterval = TimeSpan.FromSeconds(_configuration.GetValue<int>("Worker:PollIntervalSeconds", 10));
        _heartbeatInterval = TimeSpan.FromSeconds(_configuration.GetValue<int>("Worker:HeartbeatIntervalSeconds", 30));
        _leaseDurationMinutes = _configuration.GetValue<int>("Worker:LeaseDurationMinutes", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerName} starting", _workerName);

        // Register with coordinator
        try
        {
            await RegisterWorkerAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register worker");
            return;
        }

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

    private async Task RegisterWorkerAsync(CancellationToken cancellationToken)
    {
        var registerRequest = new
        {
            Name = _workerName,
            HostName = Environment.MachineName,
            Capacity = 10
        };

        var response = await _httpClient.PostAsJsonAsync("/api/workers/register", registerRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var workerResponse = await response.Content.ReadFromJsonAsync<WorkerRegistrationResponse>(cancellationToken: cancellationToken);
        if (workerResponse != null)
        {
            _workerId = workerResponse.Id;
            _logger.LogInformation("Worker registered successfully with ID {WorkerId}", _workerId);
        }
    }

    private async Task SendHeartbeatsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _httpClient.PostAsync($"/api/workers/{_workerId}/heartbeat", null, cancellationToken);
                _logger.LogDebug("Worker {WorkerName} sent heartbeat", _workerName);
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
        try
        {
            // Try to acquire a job from the coordinator
            var acquireRequest = new
            {
                WorkerId = _workerId.ToString(),
                LeaseDurationMinutes = _leaseDurationMinutes
            };

            var response = await _httpClient.PostAsJsonAsync("/api/jobs/acquire", acquireRequest, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                // No jobs available
                _logger.LogDebug("No jobs available");
                return;
            }

            response.EnsureSuccessStatusCode();
            var jobResponse = await response.Content.ReadFromJsonAsync<JobDto>(cancellationToken: cancellationToken);
            
            if (jobResponse == null)
                return;

            _logger.LogInformation("Acquired job {JobId} of type {JobType}", jobResponse.Id, jobResponse.Type);

            // Convert to Job entity
            var job = new Job
            {
                Id = jobResponse.Id,
                Name = jobResponse.Name,
                Type = jobResponse.Type,
                Payload = jobResponse.Payload,
                Status = Enum.Parse<JobStatus>(jobResponse.Status),
                RetryCount = jobResponse.RetryCount,
                MaxRetries = jobResponse.MaxRetries,
                CreatedAt = jobResponse.CreatedAt,
                WorkerId = jobResponse.WorkerId
            };

            // Execute the job
            var success = await ExecuteJobAsync(job, cancellationToken);

            // Report completion
            await ReportJobCompletionAsync(job.Id, success, null, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to communicate with coordinator");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in job processing");
        }
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

    private async Task ReportJobCompletionAsync(Guid jobId, bool success, string? errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            var completeRequest = new
            {
                Success = success,
                ErrorMessage = errorMessage
            };

            var response = await _httpClient.PostAsJsonAsync($"/api/jobs/{jobId}/complete", completeRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Reported job {JobId} completion: {Success}", jobId, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report job completion for {JobId}", jobId);
        }
    }

    // DTOs for deserialization
    private class WorkerRegistrationResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class JobDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? WorkerId { get; set; }
    }
}
