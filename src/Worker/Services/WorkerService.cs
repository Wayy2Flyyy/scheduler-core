using System.Diagnostics;
using System.Text.Json;
using Shared;
using Worker.Handlers;

namespace Worker.Services;

public sealed class WorkerService : BackgroundService
{
    private readonly WorkerClient _client;
    private readonly WorkerOptions _options;
    private readonly JobHandlerRegistry _registry;
    private readonly ILogger<WorkerService> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly List<Task> _running = new();

    public WorkerService(
        WorkerClient client,
        WorkerOptions options,
        JobHandlerRegistry registry,
        ILogger<WorkerService> logger)
    {
        _client = client;
        _options = options;
        _registry = registry;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_options.MaxParallelJobs, _options.MaxParallelJobs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var heartbeatTask = RunHeartbeatAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupCompletedAsync();

            var availableSlots = _options.MaxParallelJobs - _running.Count;
            if (availableSlots <= 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                continue;
            }

            IReadOnlyCollection<JobDto> jobs;
            try
            {
                jobs = await _client.ClaimJobsAsync(_options.WorkerId, availableSlots, _options.LeaseSeconds, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to claim jobs");
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                continue;
            }

            foreach (var job in jobs)
            {
                var task = RunJobAsync(job, stoppingToken);
                _running.Add(task);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }

        await Task.WhenAll(_running);
        await heartbeatTask;
    }

    private async Task RunJobAsync(JobDto job, CancellationToken stoppingToken)
    {
        await _semaphore.WaitAsync(stoppingToken);
        using var renewCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewTask = RenewLeaseLoopAsync(job, renewCts.Token);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var timeout = TimeSpan.FromSeconds(job.TimeoutSeconds);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(timeout);

            JobHandlerResult result;

            try
            {
                if (!_registry.TryGet(job.Type, out var handler))
                {
                    result = new JobHandlerResult(false, $"Unsupported job type {job.Type}", null);
                }
                else
                {
                    result = await handler.HandleAsync(job, cts.Token);
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                result = stoppingToken.IsCancellationRequested
                    ? new JobHandlerResult(false, "Worker shutting down", null)
                    : new JobHandlerResult(false, "Job timed out", JsonSerializer.SerializeToElement(new { timeoutSeconds = job.TimeoutSeconds }));
                result = new JobHandlerResult(false, "Job timed out", JsonSerializer.SerializeToElement(new { timeoutSeconds = job.TimeoutSeconds }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId} failed", job.Id);
                result = new JobHandlerResult(false, ex.Message, null);
            }

            stopwatch.Stop();
            var completion = new JobCompletionRequest(
                _options.WorkerId,
                job.Id,
                result.Success,
                result.Error,
                result.Result,
                (int)stopwatch.ElapsedMilliseconds);

            try
            {
                await _client.CompleteJobAsync(completion, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to report completion for job {JobId}", job.Id);
            }
        }
        finally
        {
            renewCts.Cancel();
            try
            {
                await renewTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lease renewal task failed for job {JobId}", job.Id);
            }
            await _client.CompleteJobAsync(completion, stoppingToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task RunHeartbeatAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _client.RecordHeartbeatAsync(_options.WorkerId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send heartbeat");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds), stoppingToken);
        }
    }

    private async Task CleanupCompletedAsync()
    {
        if (_running.Count == 0)
        {
            return;
        }

        var completed = _running.Where(task => task.IsCompleted).ToList();
        if (completed.Count == 0)
        {
            return;
        }

        foreach (var task in completed)
        {
            _running.Remove(task);
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job task failed");
            }
        }
    }
}
