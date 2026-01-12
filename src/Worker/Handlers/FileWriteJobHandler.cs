using System.Text.Json;
using Shared;
using Worker.Services;

namespace Worker.Handlers;

public sealed class FileWriteJobHandler : IJobHandler
{
    private readonly WorkerOptions _options;

    public FileWriteJobHandler(WorkerOptions options)
    {
        _options = options;
    }

    public string Type => JobTypes.FileWrite;

    public async Task<JobHandlerResult> HandleAsync(JobDto job, CancellationToken cancellationToken)
    {
        var payload = job.Payload;
        var fileName = payload.TryGetProperty("fileName", out var fileElement)
            ? fileElement.GetString()
            : null;
        var content = payload.TryGetProperty("content", out var contentElement)
            ? contentElement.GetString()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new JobHandlerResult(false, "Payload must include fileName", null);
        }

        Directory.CreateDirectory(_options.OutputDirectory);
        var path = Path.Combine(_options.OutputDirectory, fileName);
        await File.WriteAllTextAsync(path, content ?? string.Empty, cancellationToken);

        var result = JsonSerializer.SerializeToElement(new { path });
        return new JobHandlerResult(true, null, result);
    }
}
