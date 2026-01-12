namespace Worker.Services;

public sealed class WorkerOptions
{
    public string CoordinatorUrl { get; set; } = "http://localhost:5000";
    public string WorkerId { get; set; } = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}";
    public int PollIntervalSeconds { get; set; } = 5;
    public int LeaseSeconds { get; set; } = 60;
    public int MaxParallelJobs { get; set; } = 4;
    public int HeartbeatIntervalSeconds { get; set; } = 15;
    public string OutputDirectory { get; set; } = "./worker-output";
}
