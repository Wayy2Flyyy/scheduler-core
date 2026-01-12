namespace Coordinator.Models;

public sealed class WorkerHeartbeatEntity
{
    public Guid Id { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public DateTimeOffset LastSeenAt { get; set; }
}
