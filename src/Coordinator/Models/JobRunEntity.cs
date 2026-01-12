using Shared;

namespace Coordinator.Models;

public sealed class JobRunEntity
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public JobEntity? Job { get; set; }
    public int Attempt { get; set; }
    public JobStatus Status { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? DurationMs { get; set; }
    public string? Error { get; set; }
    public string? Result { get; set; }
}
