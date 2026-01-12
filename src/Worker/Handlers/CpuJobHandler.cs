using System.Diagnostics;
using System.Text.Json;
using Shared;

namespace Worker.Handlers;

public sealed class CpuJobHandler : IJobHandler
{
    public string Type => JobTypes.Cpu;

    public Task<JobHandlerResult> HandleAsync(JobDto job, CancellationToken cancellationToken)
    {
        var payload = job.Payload;
        var durationSeconds = payload.TryGetProperty("durationSeconds", out var durationElement)
            ? Math.Max(1, durationElement.GetInt32())
            : 2;

        var stopwatch = Stopwatch.StartNew();
        var target = TimeSpan.FromSeconds(durationSeconds);
        var iterations = 0L;

        while (stopwatch.Elapsed < target)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iterations += 1;
        }

        stopwatch.Stop();

        var result = JsonSerializer.SerializeToElement(new
        {
            durationMs = stopwatch.ElapsedMilliseconds,
            iterations
        });

        return Task.FromResult(new JobHandlerResult(true, null, result));
    }
}
