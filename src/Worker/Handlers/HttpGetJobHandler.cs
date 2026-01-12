using System.Text.Json;
using Shared;

namespace Worker.Handlers;

public sealed class HttpGetJobHandler : IJobHandler
{
    private readonly HttpClient _httpClient;

    public HttpGetJobHandler(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Type => JobTypes.HttpGet;

    public async Task<JobHandlerResult> HandleAsync(JobDto job, CancellationToken cancellationToken)
    {
        var payload = job.Payload;
        if (!payload.TryGetProperty("url", out var urlElement))
        {
            return new JobHandlerResult(false, "Payload must include url", null);
        }

        var url = urlElement.GetString();
        if (string.IsNullOrWhiteSpace(url))
        {
            return new JobHandlerResult(false, "URL is empty", null);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _httpClient.GetAsync(url, cancellationToken);
        stopwatch.Stop();
        var resultJson = JsonSerializer.SerializeToElement(new
        {
            statusCode = (int)response.StatusCode,
            reason = response.ReasonPhrase,
            latencyMs = stopwatch.ElapsedMilliseconds
        var response = await _httpClient.GetAsync(url, cancellationToken);
        var resultJson = JsonSerializer.SerializeToElement(new
        {
            statusCode = (int)response.StatusCode,
            reason = response.ReasonPhrase
        });

        return response.IsSuccessStatusCode
            ? new JobHandlerResult(true, null, resultJson)
            : new JobHandlerResult(false, $"HTTP {(int)response.StatusCode}", resultJson);
    }
}
