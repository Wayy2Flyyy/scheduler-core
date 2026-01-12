using System.Text.Json;
using Coordinator.Data;
using Coordinator.Metrics;
using Coordinator.Models;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace Coordinator.Services;

public sealed class JobService
{
    private readonly SchedulerDbContext _dbContext;
    private readonly CoordinatorOptions _options;
    private readonly MetricsRegistry _metrics;
    private readonly ILogger<JobService> _logger;

    public JobService(
        SchedulerDbContext dbContext,
        CoordinatorOptions options,
        MetricsRegistry metrics,
        ILogger<JobService> logger)
    {
        _dbContext = dbContext;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<JobSubmissionResponse> SubmitJobAsync(JobSubmissionRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Cron))
        {
            return await CreateRecurringJobAsync(request, now, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _dbContext.Jobs.AsNoTracking()
                .FirstOrDefaultAsync(job => job.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return new JobSubmissionResponse(existing.Id, null, existing.RunAt);
            }
        }

        var runAt = request.RunAt ?? now;
        var entity = new JobEntity
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Payload = request.Payload.GetRawText(),
            Status = runAt <= now ? JobStatus.Pending : JobStatus.Scheduled,
            CreatedAt = now,
            UpdatedAt = now,
            RunAt = runAt,
            Attempts = 0,
            MaxAttempts = request.MaxAttempts ?? _options.MaxAttemptsDefault,
            TimeoutSeconds = request.TimeoutSeconds ?? _options.DefaultTimeoutSeconds,
            IdempotencyKey = request.IdempotencyKey
        };

        _dbContext.Jobs.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _metrics.Increment("jobs_submitted");
        _logger.LogInformation("Job submitted {JobId} type {JobType}", entity.Id, entity.Type);

        return new JobSubmissionResponse(entity.Id, null, entity.RunAt);
    }

    public async Task<JobDto?> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        return job is null ? null : ToDto(job);
    }

    public async Task<JobListResponse> ListJobsAsync(JobStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = _dbContext.Jobs.AsNoTracking();
        if (status.HasValue)
        {
            query = query.Where(job => job.Status == status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var jobs = await query
            .OrderByDescending(job => job.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new JobListResponse(jobs.Select(ToDto).ToList(), total);
    }

    public async Task<bool> CancelJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return false;
        }

        if (job.Status is JobStatus.Succeeded or JobStatus.Failed or JobStatus.DeadLetter)
        {
            return false;
        }

        job.Status = JobStatus.Cancelled;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;

        var run = await _dbContext.JobRuns
            .Where(r => r.JobId == job.Id && r.Status == JobStatus.Running)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is not null)
        {
            run.Status = JobStatus.Cancelled;
            run.CompletedAt = job.UpdatedAt;
            run.Error = "Cancelled";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _metrics.Increment("jobs_cancelled");
        return true;
    }

    public async Task<bool> RetryJobAsync(Guid jobId, string? reason, CancellationToken cancellationToken)
    {
        var job = await _dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return false;
        }

        if (job.Status is JobStatus.Succeeded or JobStatus.Cancelled)
        {
            return false;
        }

        job.Status = JobStatus.Pending;
        job.RunAt = DateTimeOffset.UtcNow;
        job.LastError = reason;
        job.Attempts = 0;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _metrics.Increment("jobs_retried");
        return true;
    }

    public async Task<ClaimJobsResponse> ClaimJobsAsync(ClaimJobsRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var leaseExpiresAt = now.AddSeconds(request.LeaseSeconds);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var claimedJobs = await _dbContext.Jobs
            .FromSqlRaw(@"
                SELECT * FROM jobs
                WHERE status IN (0, 1)
                  AND run_at <= NOW()
                  AND attempts < max_attempts
                  AND (lease_expires_at IS NULL OR lease_expires_at < NOW())
                ORDER BY run_at ASC
                LIMIT {0}
                FOR UPDATE SKIP LOCKED", request.MaxJobs)
            .ToListAsync(cancellationToken);

        foreach (var job in claimedJobs)
        {
            job.Attempts += 1;
            job.Status = JobStatus.Running;
            job.LeaseOwner = request.WorkerId;
            job.LeaseExpiresAt = leaseExpiresAt;
            job.UpdatedAt = now;

            _dbContext.JobRuns.Add(new JobRunEntity
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                Attempt = job.Attempts,
                Status = JobStatus.Running,
                WorkerId = request.WorkerId,
                StartedAt = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (claimedJobs.Count > 0)
        {
            _metrics.Increment("jobs_claimed", claimedJobs.Count);
            _logger.LogInformation("Worker {WorkerId} claimed {Count} jobs", request.WorkerId, claimedJobs.Count);
        }

        return new ClaimJobsResponse(claimedJobs.Select(ToDto).ToList());
    }

    public async Task<bool> RecordHeartbeatAsync(WorkerHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var timestamp = request.Timestamp ?? DateTimeOffset.UtcNow;
        var existing = await _dbContext.WorkerHeartbeats.FirstOrDefaultAsync(w => w.WorkerId == request.WorkerId, cancellationToken);
        if (existing is null)
        {
            _dbContext.WorkerHeartbeats.Add(new WorkerHeartbeatEntity
            {
                Id = Guid.NewGuid(),
                WorkerId = request.WorkerId,
                LastSeenAt = timestamp
            });
        }
        else
        {
            existing.LastSeenAt = timestamp;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteJobAsync(JobCompletionRequest request, CancellationToken cancellationToken)
    {
        var job = await _dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken);
        if (job is null)
        {
            return false;
        }

        if (job.Status != JobStatus.Running)
        {
            _logger.LogWarning("Completion ignored for job {JobId} with status {Status}", job.Id, job.Status);
            return false;
        }

        if (!string.Equals(job.LeaseOwner, request.WorkerId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Worker {WorkerId} attempted to complete job {JobId} without lease", request.WorkerId, job.Id);
            return false;
        }

        job.UpdatedAt = DateTimeOffset.UtcNow;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;

        if (request.Success)
        {
            job.Status = JobStatus.Succeeded;
            job.Result = request.Result?.GetRawText();
            job.LastError = null;
            _metrics.Increment("jobs_succeeded");
        }
        else
        {
            job.LastError = request.Error;
            job.Result = request.Result?.GetRawText();

            if (job.Attempts >= job.MaxAttempts)
            {
                job.Status = JobStatus.DeadLetter;
                _metrics.Increment("jobs_deadlettered");
            }
            else
            {
                var nextRun = RetryPolicy.CalculateNextRun(DateTimeOffset.UtcNow, job.Attempts);
                job.RunAt = nextRun;
                job.Status = nextRun <= DateTimeOffset.UtcNow ? JobStatus.Pending : JobStatus.Scheduled;
                _metrics.Increment("jobs_failed");
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> RequeueExpiredLeasesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await _dbContext.Jobs
            .Where(job => job.Status == JobStatus.Running && job.LeaseExpiresAt < now)
            .ToListAsync(cancellationToken);

        foreach (var job in expired)
        {
            job.LeaseOwner = null;
            job.LeaseExpiresAt = null;
            job.LastError = "Lease expired";
            if (job.Attempts >= job.MaxAttempts)
            {
                job.Status = JobStatus.DeadLetter;
            }
            else
            {
                var nextRun = RetryPolicy.CalculateNextRun(now, job.Attempts);
                job.RunAt = nextRun;
                job.Status = nextRun <= now ? JobStatus.Pending : JobStatus.Scheduled;
            }

            job.UpdatedAt = now;

            var run = await _dbContext.JobRuns
                .Where(r => r.JobId == job.Id && r.Status == JobStatus.Running)
                .OrderByDescending(r => r.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (run is not null)
            {
                run.Status = JobStatus.Failed;
                run.CompletedAt = now;
                run.Error = "Lease expired";
            }
        }

        if (expired.Count > 0)
        {
            _metrics.Increment("jobs_lease_expired", expired.Count);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return expired.Count;
    }

    public async Task<int> EnqueueDueRecurringJobsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var dueRecurring = await _dbContext.RecurringJobs
            .Where(job => job.IsActive && job.NextRunAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var recurring in dueRecurring)
        {
            var job = new JobEntity
            {
                Id = Guid.NewGuid(),
                Type = recurring.Type,
                Payload = recurring.Payload,
                Status = JobStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                RunAt = now,
                Attempts = 0,
                MaxAttempts = _options.MaxAttemptsDefault,
                TimeoutSeconds = _options.DefaultTimeoutSeconds,
                RecurringJobId = recurring.Id
            };

            _dbContext.Jobs.Add(job);

            var nextRun = CronSchedule.GetNextOccurrence(recurring.CronExpression, now);
            recurring.NextRunAt = nextRun ?? now.AddMinutes(1);
            recurring.UpdatedAt = now;
        }

        if (dueRecurring.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _metrics.Increment("recurring_jobs_enqueued", dueRecurring.Count);
        }

        return dueRecurring.Count;
    }

    private async Task<JobSubmissionResponse> CreateRecurringJobAsync(
        JobSubmissionRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _dbContext.RecurringJobs.AsNoTracking()
                .FirstOrDefaultAsync(job => job.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return new JobSubmissionResponse(null, existing.Id, existing.NextRunAt);
            }
        }

        var nextRunAt = CronSchedule.GetNextOccurrence(request.Cron!, now) ?? now.AddMinutes(1);
        var recurring = new RecurringJobEntity
        {
            Id = Guid.NewGuid(),
            Name = $"recurring-{request.Type}-{Guid.NewGuid():N}",
            CronExpression = request.Cron!,
            Type = request.Type,
            Payload = request.Payload.GetRawText(),
            NextRunAt = nextRunAt,
            IsActive = true,
            IdempotencyKey = request.IdempotencyKey,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.RecurringJobs.Add(recurring);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _metrics.Increment("recurring_jobs_created");
        return new JobSubmissionResponse(null, recurring.Id, recurring.NextRunAt);
    }

    public static JobDto ToDto(JobEntity job)
    {
        return new JobDto(
            job.Id,
            job.Type,
            job.Status,
            job.CreatedAt,
            job.UpdatedAt,
            job.RunAt,
            job.Attempts,
            job.MaxAttempts,
            job.TimeoutSeconds,
            job.LeaseOwner,
            job.LeaseExpiresAt,
            job.IdempotencyKey,
            job.LastError,
            job.Result is null ? null : JsonSerializer.Deserialize<JsonElement>(job.Result),
            job.RecurringJobId,
            JsonSerializer.Deserialize<JsonElement>(job.Payload));
    }

    public async Task<JobRunListResponse> GetJobRunsAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var runs = await _dbContext.JobRuns.AsNoTracking()
            .Where(run => run.JobId == jobId)
            .OrderByDescending(run => run.StartedAt)
            .ToListAsync(cancellationToken);

        var dtos = runs.Select(run => new JobRunDto(
            run.Id,
            run.JobId,
            run.Attempt,
            run.Status,
            run.WorkerId,
            run.StartedAt,
            run.CompletedAt,
            run.DurationMs,
            run.Error,
            run.Result is null ? null : JsonSerializer.Deserialize<JsonElement>(run.Result)));

        return new JobRunListResponse(dtos.ToList());
    }
}
