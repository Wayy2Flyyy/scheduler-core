namespace Coordinator.Services;

public sealed class CoordinatorOptions
{
    public int DefaultLeaseSeconds { get; set; } = 60;
    public int DefaultTimeoutSeconds { get; set; } = 60;
    public int MaxAttemptsDefault { get; set; } = 5;
    public int SchedulerSweepSeconds { get; set; } = 10;
    public int RecurringSweepSeconds { get; set; } = 30;
}
