namespace SchedulerCore.Coordinator.DTOs;

public class CreateJobRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int MaxRetries { get; set; } = 3;
    public int Priority { get; set; } = 0;
    public DateTime? ScheduledAt { get; set; }
}

public class JobResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? WorkerId { get; set; }
    public string? LastError { get; set; }
    public int Priority { get; set; }
}

public class WorkerStatusResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public int Capacity { get; set; }
    public int ActiveJobs { get; set; }
}
