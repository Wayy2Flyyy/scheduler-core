using System.Text.Json;

namespace Shared;

public sealed record JobSubmissionRequest(
    string Type,
    JsonElement Payload,
    DateTimeOffset? RunAt,
    string? Cron,
    int? MaxAttempts,
    int? TimeoutSeconds,
    string? IdempotencyKey);

public sealed record JobSubmissionResponse(
    Guid? JobId,
    Guid? RecurringJobId,
    DateTimeOffset? NextRunAt);

public sealed record JobDto(
    Guid Id,
    string Type,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset RunAt,
    int Attempts,
    int MaxAttempts,
    int TimeoutSeconds,
    string? LeaseOwner,
    DateTimeOffset? LeaseExpiresAt,
    string? IdempotencyKey,
    string? LastError,
    JsonElement? Result,
    Guid? RecurringJobId,
    JsonElement Payload);

public sealed record JobListResponse(IReadOnlyCollection<JobDto> Jobs, int TotalCount);

public sealed record ClaimJobsRequest(string WorkerId, int MaxJobs, int LeaseSeconds);

public sealed record ClaimJobsResponse(IReadOnlyCollection<JobDto> Jobs);

public sealed record RenewLeaseRequest(string WorkerId, Guid JobId, int LeaseSeconds);

public sealed record RenewLeaseResponse(bool Renewed, DateTimeOffset? LeaseExpiresAt);

public sealed record JobCompletionRequest(
    string WorkerId,
    Guid JobId,
    bool Success,
    string? Error,
    JsonElement? Result,
    int DurationMs);

public sealed record WorkerHeartbeatRequest(string WorkerId, DateTimeOffset? Timestamp);

public sealed record RetryJobRequest(string? Reason);

public sealed record JobRunDto(
    Guid Id,
    Guid JobId,
    int Attempt,
    JobStatus Status,
    string WorkerId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? DurationMs,
    string? Error,
    JsonElement? Result);

public sealed record JobRunListResponse(IReadOnlyCollection<JobRunDto> Runs);
