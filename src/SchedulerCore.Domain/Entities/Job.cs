namespace SchedulerCore.Domain.Entities;

/// <summary>
/// Represents a scheduled job with its execution state
/// </summary>
public class Job
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? WorkerId { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? LastError { get; set; }
    public int Priority { get; set; }
}

public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
