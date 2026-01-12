using Coordinator.Services;

namespace Coordinator.HostedServices;

public sealed class LeaseMonitorService : BackgroundService
{
    private readonly JobService _jobService;
    private readonly CoordinatorOptions _options;
    private readonly ILogger<LeaseMonitorService> _logger;

    public LeaseMonitorService(JobService jobService, CoordinatorOptions options, ILogger<LeaseMonitorService> logger)
    {
        _jobService = jobService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var count = await _jobService.RequeueExpiredLeasesAsync(stoppingToken);
                if (count > 0)
                {
                    _logger.LogWarning("Requeued {Count} jobs after lease expiration", count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to requeue expired leases");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.SchedulerSweepSeconds), stoppingToken);
        }
    }
}
