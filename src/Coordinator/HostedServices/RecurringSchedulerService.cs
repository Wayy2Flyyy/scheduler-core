using Coordinator.Services;

namespace Coordinator.HostedServices;

public sealed class RecurringSchedulerService : BackgroundService
{
    private readonly JobService _jobService;
    private readonly CoordinatorOptions _options;
    private readonly ILogger<RecurringSchedulerService> _logger;

    public RecurringSchedulerService(JobService jobService, CoordinatorOptions options, ILogger<RecurringSchedulerService> logger)
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
                var count = await _jobService.EnqueueDueRecurringJobsAsync(stoppingToken);
                if (count > 0)
                {
                    _logger.LogInformation("Enqueued {Count} recurring jobs", count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue recurring jobs");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.RecurringSweepSeconds), stoppingToken);
        }
    }
}
