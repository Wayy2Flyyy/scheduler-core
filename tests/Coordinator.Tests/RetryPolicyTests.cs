using FluentAssertions;
using Shared;
using Xunit;

namespace Coordinator.Tests;

public sealed class RetryPolicyTests
{
    [Fact]
    public void CalculateNextRun_UsesExponentialBackoff()
    {
        var now = DateTimeOffset.UtcNow;

        var first = RetryPolicy.CalculateNextRun(now, 1, baseDelaySeconds: 5, maxDelaySeconds: 300);
        var second = RetryPolicy.CalculateNextRun(now, 2, baseDelaySeconds: 5, maxDelaySeconds: 300);

        second.Should().BeAfter(first);
        first.Should().BeAfter(now);
    }
}
