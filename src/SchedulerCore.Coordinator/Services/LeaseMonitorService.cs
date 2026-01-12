using SchedulerCore.Domain.Interfaces;

namespace SchedulerCore.Coordinator.Services;

/// <summary>
/// Background service that monitors job leases and releases expired ones
/// </summary>
public class LeaseMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<LeaseMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public LeaseMonitorService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<LeaseMonitorService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Lease Monitor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpiredLeasesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking expired leases");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Lease Monitor Service stopped");
    }

    private async Task CheckExpiredLeasesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        var expiredJobs = await jobRepository.GetExpiredLeasesAsync(cancellationToken);

        foreach (var job in expiredJobs)
        {
            _logger.LogWarning("Releasing expired lease for job {JobId} from worker {WorkerId}", 
                job.Id, job.WorkerId);
            
            await jobRepository.ReleaseLeaseAsync(job.Id, cancellationToken);
        }

        if (expiredJobs.Count > 0)
        {
            _logger.LogInformation("Released {Count} expired leases", expiredJobs.Count);
        }
    }
}
