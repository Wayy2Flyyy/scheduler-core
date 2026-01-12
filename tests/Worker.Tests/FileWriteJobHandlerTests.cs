using System.Text.Json;
using FluentAssertions;
using Shared;
using Worker.Handlers;
using Worker.Services;
using Xunit;

namespace Worker.Tests;

public sealed class FileWriteJobHandlerTests
{
    [Fact]
    public async Task HandleAsync_WritesFile()
    {
        var options = new WorkerOptions { OutputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) };
        var handler = new FileWriteJobHandler(options);

        var payload = JsonSerializer.SerializeToElement(new { fileName = "output.txt", content = "hello" });
        var job = new JobDto(
            Guid.NewGuid(),
            JobTypes.FileWrite,
            JobStatus.Running,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            0,
            3,
            30,
            null,
            null,
            null,
            null,
            null,
            null,
            payload);

        var result = await handler.HandleAsync(job, CancellationToken.None);
        result.Success.Should().BeTrue();
        File.Exists(Path.Combine(options.OutputDirectory, "output.txt")).Should().BeTrue();
    }
}
