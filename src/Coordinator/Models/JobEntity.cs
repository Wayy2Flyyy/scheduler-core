using Shared;

namespace Coordinator.Models;

public sealed class JobEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public JobStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset RunAt { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; }
    public int TimeoutSeconds { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? LastError { get; set; }
    public string? Result { get; set; }
    public Guid? RecurringJobId { get; set; }
    public RecurringJobEntity? RecurringJob { get; set; }
}
