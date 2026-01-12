using Coordinator.Data;
using Coordinator.Metrics;
using Coordinator.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Coordinator.Tests;

public sealed class LeaseAcquisitionTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public LeaseAcquisitionTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ClaimJobs_OnlyReturnsAvailableLease()
    {
        await TestDatabaseHelper.ApplyMigrationsAsync(_fixture.ConnectionString);

        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var dbContext = new SchedulerDbContext(options);
        var service = new JobService(dbContext, new CoordinatorOptions(), new MetricsRegistry(), NullLogger<JobService>.Instance);

        var request = new JobSubmissionRequest(
            JobTypes.Cpu,
            System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"durationSeconds\":1}"),
            DateTimeOffset.UtcNow,
            null,
            3,
            10,
            null);

        var job = await service.SubmitJobAsync(request, CancellationToken.None);

        var claimOne = await service.ClaimJobsAsync(new ClaimJobsRequest("worker-1", 1, 60), CancellationToken.None);
        var claimTwo = await service.ClaimJobsAsync(new ClaimJobsRequest("worker-2", 1, 60), CancellationToken.None);

        claimOne.Jobs.Should().ContainSingle(j => j.Id == job.JobId);
        claimTwo.Jobs.Should().BeEmpty();
    }
}
