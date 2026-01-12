using System.Net.Http.Json;
using Shared;

namespace Worker.Services;

public sealed class WorkerClient
{
    private readonly HttpClient _httpClient;

    public WorkerClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyCollection<JobDto>> ClaimJobsAsync(string workerId, int maxJobs, int leaseSeconds, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/workers/claim", new ClaimJobsRequest(workerId, maxJobs, leaseSeconds), cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ClaimJobsResponse>(cancellationToken: cancellationToken);
        return payload?.Jobs ?? Array.Empty<JobDto>();
    }

    public async Task RecordHeartbeatAsync(string workerId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/workers/heartbeat", new WorkerHeartbeatRequest(workerId, DateTimeOffset.UtcNow), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CompleteJobAsync(JobCompletionRequest request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/workers/complete", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<RenewLeaseResponse> RenewLeaseAsync(RenewLeaseRequest request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/workers/renew", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<RenewLeaseResponse>(cancellationToken: cancellationToken);
        return payload ?? new RenewLeaseResponse(false, null);
    }
}
