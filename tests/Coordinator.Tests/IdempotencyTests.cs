using Coordinator.Data;
using Coordinator.Metrics;
using Coordinator.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Coordinator.Tests;

public sealed class IdempotencyTests
{
    [Fact]
    public async Task SubmitJob_WithSameIdempotencyKey_ReturnsExistingJob()
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
            "idem-key");

        var first = await service.SubmitJobAsync(request, CancellationToken.None);
        var second = await service.SubmitJobAsync(request, CancellationToken.None);

        second.JobId.Should().Be(first.JobId);
    }
}
