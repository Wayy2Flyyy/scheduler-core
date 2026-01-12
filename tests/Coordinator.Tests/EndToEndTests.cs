using Coordinator.Data;
using Coordinator.Metrics;
using Coordinator.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Worker.Handlers;
using Xunit;

namespace Coordinator.Tests;

public sealed class EndToEndTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EndToEndTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SubmitJob_WorkerExecutes_StatusUpdates()
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

        var response = await service.SubmitJobAsync(request, CancellationToken.None);

        var claim = await service.ClaimJobsAsync(new ClaimJobsRequest("worker-test", 1, 60), CancellationToken.None);
        claim.Jobs.Should().ContainSingle();

        var handler = new CpuJobHandler();
        var result = await handler.HandleAsync(claim.Jobs.Single(), CancellationToken.None);

        var completion = new JobCompletionRequest(
            "worker-test",
            response.JobId!.Value,
            result.Success,
            result.Error,
            result.Result,
            100);

        var completed = await service.CompleteJobAsync(completion, CancellationToken.None);
        completed.Should().BeTrue();

        var job = await service.GetJobAsync(response.JobId!.Value, CancellationToken.None);
        job!.Status.Should().Be(JobStatus.Succeeded);
        job.Result.Should().NotBeNull();
    }
}
