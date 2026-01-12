using Coordinator.Data;
using Coordinator.Metrics;
using Coordinator.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Coordinator.Tests;

public sealed class RetryDeadLetterTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RetryDeadLetterTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FailedJob_ReachesDeadLetterAfterMaxAttempts()
    {
        await TestDatabaseHelper.ApplyMigrationsAsync(_fixture.ConnectionString);

        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var dbContext = new SchedulerDbContext(options);
        var service = new JobService(dbContext, new CoordinatorOptions { MaxAttemptsDefault = 2 }, new MetricsRegistry(), NullLogger<JobService>.Instance);

        var request = new JobSubmissionRequest(
            JobTypes.Cpu,
            System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"durationSeconds\":1}"),
            DateTimeOffset.UtcNow,
            null,
            2,
            10,
            null);

        var response = await service.SubmitJobAsync(request, CancellationToken.None);

        var firstClaim = await service.ClaimJobsAsync(new ClaimJobsRequest("worker-1", 1, 60), CancellationToken.None);
        var completion1 = new JobCompletionRequest("worker-1", response.JobId!.Value, false, "fail", null, 10);
        await service.CompleteJobAsync(completion1, CancellationToken.None);

        var jobEntity = await dbContext.Jobs.FirstAsync(j => j.Id == response.JobId, CancellationToken.None);
        jobEntity.RunAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        jobEntity.Status = JobStatus.Pending;
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var secondClaim = await service.ClaimJobsAsync(new ClaimJobsRequest("worker-1", 1, 60), CancellationToken.None);
        secondClaim.Jobs.Should().ContainSingle(j => j.Id == response.JobId);

        var completion2 = new JobCompletionRequest("worker-1", response.JobId!.Value, false, "fail", null, 10);
        await service.CompleteJobAsync(completion2, CancellationToken.None);

        var job = await service.GetJobAsync(response.JobId!.Value, CancellationToken.None);
        job!.Status.Should().Be(JobStatus.DeadLetter);
        job.Attempts.Should().Be(2);
    }
}
