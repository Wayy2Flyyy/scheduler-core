using Coordinator.Data;
using Coordinator.Metrics;
using Coordinator.Models;
using Coordinator.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Coordinator.Tests;

public sealed class SchedulingTests
{
    [Fact]
    public async Task SubmitJob_Immediate_SetsPending()
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new SchedulerDbContext(options);
        var service = new JobService(db, new CoordinatorOptions(), new MetricsRegistry(), NullLogger<JobService>.Instance);

        var request = new JobSubmissionRequest(
            JobTypes.Cpu,
            System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"durationSeconds\":1}"),
            DateTimeOffset.UtcNow,
            null,
            3,
            10,
            null);

        var response = await service.SubmitJobAsync(request, CancellationToken.None);
        var job = await service.GetJobAsync(response.JobId!.Value, CancellationToken.None);

        job!.Status.Should().Be(JobStatus.Pending);
    }

    [Fact]
    public async Task SubmitJob_Delayed_SetsScheduled()
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new SchedulerDbContext(options);
        var service = new JobService(db, new CoordinatorOptions(), new MetricsRegistry(), NullLogger<JobService>.Instance);

        var request = new JobSubmissionRequest(
            JobTypes.Cpu,
            System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"durationSeconds\":1}"),
            DateTimeOffset.UtcNow.AddMinutes(5),
            null,
            3,
            10,
            null);

        var response = await service.SubmitJobAsync(request, CancellationToken.None);
        var job = await service.GetJobAsync(response.JobId!.Value, CancellationToken.None);

        job!.Status.Should().Be(JobStatus.Scheduled);
    }

    [Fact]
    public async Task SubmitJob_Cron_CreatesRecurringJob()
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new SchedulerDbContext(options);
        var service = new JobService(db, new CoordinatorOptions(), new MetricsRegistry(), NullLogger<JobService>.Instance);

        var request = new JobSubmissionRequest(
            JobTypes.Cpu,
            System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"durationSeconds\":1}"),
            null,
            "*/5 * * * *",
            3,
            10,
            "recurring-test");

        var response = await service.SubmitJobAsync(request, CancellationToken.None);
        response.RecurringJobId.Should().NotBeNull();

        var recurring = await db.RecurringJobs.FirstOrDefaultAsync(r => r.Id == response.RecurringJobId, CancellationToken.None);
        recurring.Should().NotBeNull();
    }

    [Fact]
    public async Task RequeueExpiredLease_ReleasesLeaseAndSchedulesRetry()
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new SchedulerDbContext(options);
        var service = new JobService(db, new CoordinatorOptions(), new MetricsRegistry(), NullLogger<JobService>.Instance);

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            Type = JobTypes.Cpu,
            Payload = "{}",
            Status = JobStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            RunAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            Attempts = 1,
            MaxAttempts = 3,
            TimeoutSeconds = 10,
            LeaseOwner = "worker-1",
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-5)
        };
        db.Jobs.Add(job);
        db.JobRuns.Add(new JobRunEntity
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Attempt = 1,
            Status = JobStatus.Running,
            WorkerId = "worker-1",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        await service.RequeueExpiredLeasesAsync(CancellationToken.None);

        var updated = await db.Jobs.FirstAsync(j => j.Id == job.Id, CancellationToken.None);
        updated.Status.Should().Be(JobStatus.Scheduled);
        updated.LeaseOwner.Should().BeNull();
        updated.LeaseExpiresAt.Should().BeNull();
        updated.LastError.Should().Be("Lease expired");

        var run = await db.JobRuns.FirstAsync(r => r.JobId == job.Id, CancellationToken.None);
        run.Status.Should().Be(JobStatus.Failed);
        run.CompletedAt.Should().NotBeNull();
    }
}
