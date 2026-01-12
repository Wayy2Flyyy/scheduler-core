using SchedulerCore.Domain.Entities;
using SchedulerCore.Domain.Interfaces;

namespace SchedulerCore.Coordinator.Services;

/// <summary>
/// Background service that monitors worker heartbeats and marks dead workers
/// </summary>
public class WorkerHealthMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<WorkerHealthMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromMinutes(2);

    public WorkerHealthMonitorService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<WorkerHealthMonitorService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker Health Monitor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckWorkerHealthAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking worker health");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Worker Health Monitor Service stopped");
    }

    private async Task CheckWorkerHealthAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var workerRepository = scope.ServiceProvider.GetRequiredService<IWorkerRepository>();

        var deadWorkers = await workerRepository.GetDeadWorkersAsync(_heartbeatTimeout, cancellationToken);

        foreach (var worker in deadWorkers)
        {
            _logger.LogWarning("Worker {WorkerId} ({WorkerName}) is dead - last heartbeat at {LastHeartbeat}", 
                worker.Id, worker.Name, worker.LastHeartbeat);
            
            worker.Status = WorkerStatus.Dead;
            await workerRepository.UpdateAsync(worker, cancellationToken);
        }

        if (deadWorkers.Count > 0)
        {
            _logger.LogInformation("Marked {Count} workers as dead", deadWorkers.Count);
        }
    }
}
