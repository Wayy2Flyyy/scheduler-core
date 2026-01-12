using Microsoft.EntityFrameworkCore;
using SchedulerCore.Domain.Entities;
using SchedulerCore.Persistence;
using SchedulerCore.Persistence.Repositories;

namespace SchedulerCore.Tests;

public class JobRepositoryTests
{
    private SchedulerDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SchedulerDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateJob()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new JobRepository(context);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = "Test Job",
            Type = "test",
            Payload = "test payload",
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            MaxRetries = 3,
            Priority = 0
        };

        // Act
        var result = await repository.CreateAsync(job);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(job.Id, result.Id);
        
        var retrievedJob = await repository.GetByIdAsync(job.Id);
        Assert.NotNull(retrievedJob);
        Assert.Equal(job.Name, retrievedJob.Name);
    }

    [Fact]
    public async Task GetPendingJobsAsync_ShouldReturnJobsOrderedByPriority()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new JobRepository(context);
        
        var lowPriorityJob = new Job
        {
            Id = Guid.NewGuid(),
            Name = "Low Priority",
            Type = "test",
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Priority = 1
        };
        
        var highPriorityJob = new Job
        {
            Id = Guid.NewGuid(),
            Name = "High Priority",
            Type = "test",
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Priority = 10
        };

        await repository.CreateAsync(lowPriorityJob);
        await repository.CreateAsync(highPriorityJob);

        // Act
        var jobs = await repository.GetPendingJobsAsync(10);

        // Assert
        Assert.Equal(2, jobs.Count);
        Assert.Equal(highPriorityJob.Id, jobs[0].Id);
        Assert.Equal(lowPriorityJob.Id, jobs[1].Id);
    }

    [Fact]
    public async Task AcquireNextJobAsync_ShouldAssignJobToWorker()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new JobRepository(context);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = "Test Job",
            Type = "test",
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await repository.CreateAsync(job);

        // Act
        var workerId = "worker-123";
        var leaseExpiration = DateTime.UtcNow.AddMinutes(5);
        var acquiredJob = await repository.AcquireNextJobAsync(workerId, leaseExpiration);

        // Assert
        Assert.NotNull(acquiredJob);
        Assert.Equal(job.Id, acquiredJob.Id);
        Assert.Equal(JobStatus.Running, acquiredJob.Status);
        Assert.Equal(workerId, acquiredJob.WorkerId);
        Assert.NotNull(acquiredJob.LeaseExpiresAt);
    }

    [Fact]
    public async Task GetExpiredLeasesAsync_ShouldReturnExpiredJobs()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new JobRepository(context);
        
        var expiredJob = new Job
        {
            Id = Guid.NewGuid(),
            Name = "Expired Job",
            Type = "test",
            Status = JobStatus.Running,
            CreatedAt = DateTime.UtcNow,
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            WorkerId = "worker-123"
        };
        
        var activeJob = new Job
        {
            Id = Guid.NewGuid(),
            Name = "Active Job",
            Type = "test",
            Status = JobStatus.Running,
            CreatedAt = DateTime.UtcNow,
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(5),
            WorkerId = "worker-456"
        };

        await repository.CreateAsync(expiredJob);
        await repository.CreateAsync(activeJob);

        // Act
        var expiredJobs = await repository.GetExpiredLeasesAsync();

        // Assert
        Assert.Single(expiredJobs);
        Assert.Equal(expiredJob.Id, expiredJobs[0].Id);
    }
}
