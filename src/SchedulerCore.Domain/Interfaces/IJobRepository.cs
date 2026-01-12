using SchedulerCore.Domain.Entities;

namespace SchedulerCore.Domain.Interfaces;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Job>> GetPendingJobsAsync(int limit, CancellationToken cancellationToken = default);
    Task<List<Job>> GetJobsByStatusAsync(JobStatus status, CancellationToken cancellationToken = default);
    Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default);
    Task UpdateAsync(Job job, CancellationToken cancellationToken = default);
    Task<Job?> AcquireNextJobAsync(string workerId, DateTime leaseExpiration, CancellationToken cancellationToken = default);
    Task<List<Job>> GetExpiredLeasesAsync(CancellationToken cancellationToken = default);
    Task ReleaseLeaseAsync(Guid jobId, CancellationToken cancellationToken = default);
}
