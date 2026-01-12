namespace SchedulerCore.Domain.Entities;

/// <summary>
/// Represents a worker node in the distributed system
/// </summary>
public class Worker
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public WorkerStatus Status { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public int Capacity { get; set; }
    public int ActiveJobs { get; set; }
}

public enum WorkerStatus
{
    Active = 0,
    Idle = 1,
    Dead = 2
}
