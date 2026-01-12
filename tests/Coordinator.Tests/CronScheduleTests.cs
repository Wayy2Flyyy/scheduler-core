using FluentAssertions;
using Shared;
using Xunit;

namespace Coordinator.Tests;

public sealed class CronScheduleTests
{
    [Fact]
    public void GetNextOccurrence_ReturnsFutureTime()
    {
        var now = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var next = CronSchedule.GetNextOccurrence("*/5 * * * *", now);

        next.Should().NotBeNull();
        next!.Value.Should().BeAfter(now);
    }
}
