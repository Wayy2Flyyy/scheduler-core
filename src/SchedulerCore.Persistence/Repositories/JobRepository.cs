using Microsoft.EntityFrameworkCore;
using SchedulerCore.Domain.Entities;
using SchedulerCore.Domain.Interfaces;

namespace SchedulerCore.Persistence.Repositories;

public class JobRepository : IJobRepository
{
    private readonly SchedulerDbContext _context;

    public JobRepository(SchedulerDbContext context)
    {
        _context = context;
    }

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<List<Job>> GetPendingJobsAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .Where(j => j.Status == JobStatus.Pending)
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Job>> GetJobsByStatusAsync(JobStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Jobs
            .Where(j => j.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default)
    {
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        _context.Jobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Job?> AcquireNextJobAsync(string workerId, DateTime leaseExpiration, CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var job = await _context.Jobs
                .Where(j => j.Status == JobStatus.Pending)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (job == null)
                return null;

            job.Status = JobStatus.Running;
            job.WorkerId = workerId;
            job.LeaseExpiresAt = leaseExpiration;
            job.StartedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return job;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<Job>> GetExpiredLeasesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.Jobs
            .Where(j => j.Status == JobStatus.Running 
                && j.LeaseExpiresAt != null 
                && j.LeaseExpiresAt < now)
            .ToListAsync(cancellationToken);
    }

    public async Task ReleaseLeaseAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await GetByIdAsync(jobId, cancellationToken);
        if (job != null)
        {
            job.Status = JobStatus.Pending;
            job.WorkerId = null;
            job.LeaseExpiresAt = null;
            job.StartedAt = null;
            await UpdateAsync(job, cancellationToken);
        }
    }
}
