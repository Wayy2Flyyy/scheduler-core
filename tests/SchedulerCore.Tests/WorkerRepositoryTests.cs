using Microsoft.EntityFrameworkCore;
using SchedulerCore.Domain.Entities;
using SchedulerCore.Persistence;
using SchedulerCore.Persistence.Repositories;

namespace SchedulerCore.Tests;

public class WorkerRepositoryTests
{
    private SchedulerDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new SchedulerDbContext(options);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateWorker()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkerRepository(context);
        var worker = new Worker
        {
            Id = Guid.NewGuid(),
            Name = "test-worker",
            HostName = "localhost",
            Status = WorkerStatus.Active,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            Capacity = 10,
            ActiveJobs = 0
        };

        // Act
        var result = await repository.RegisterAsync(worker);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(worker.Id, result.Id);
        
        var retrievedWorker = await repository.GetByIdAsync(worker.Id);
        Assert.NotNull(retrievedWorker);
        Assert.Equal(worker.Name, retrievedWorker.Name);
    }

    [Fact]
    public async Task GetActiveWorkersAsync_ShouldReturnOnlyActiveWorkers()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkerRepository(context);
        
        var activeWorker = new Worker
        {
            Id = Guid.NewGuid(),
            Name = "active-worker",
            HostName = "host1",
            Status = WorkerStatus.Active,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            Capacity = 10
        };
        
        var deadWorker = new Worker
        {
            Id = Guid.NewGuid(),
            Name = "dead-worker",
            HostName = "host2",
            Status = WorkerStatus.Dead,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow.AddHours(-1),
            Capacity = 10
        };

        await repository.RegisterAsync(activeWorker);
        await repository.RegisterAsync(deadWorker);

        // Act
        var workers = await repository.GetActiveWorkersAsync();

        // Assert
        Assert.Single(workers);
        Assert.Equal(activeWorker.Id, workers[0].Id);
    }

    [Fact]
    public async Task UpdateHeartbeatAsync_ShouldUpdateLastHeartbeat()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkerRepository(context);
        var worker = new Worker
        {
            Id = Guid.NewGuid(),
            Name = "test-worker",
            HostName = "localhost",
            Status = WorkerStatus.Active,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5),
            Capacity = 10
        };

        await repository.RegisterAsync(worker);
        var originalHeartbeat = worker.LastHeartbeat;

        // Act
        await Task.Delay(100); // Small delay to ensure time difference
        await repository.UpdateHeartbeatAsync(worker.Id);

        // Assert
        var updatedWorker = await repository.GetByIdAsync(worker.Id);
        Assert.NotNull(updatedWorker);
        Assert.True(updatedWorker.LastHeartbeat > originalHeartbeat);
    }

    [Fact]
    public async Task GetDeadWorkersAsync_ShouldReturnWorkersWithExpiredHeartbeat()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkerRepository(context);
        var heartbeatTimeout = TimeSpan.FromMinutes(2);
        
        var deadWorker = new Worker
        {
            Id = Guid.NewGuid(),
            Name = "dead-worker",
            HostName = "host1",
            Status = WorkerStatus.Active,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5),
            Capacity = 10
        };
        
        var aliveWorker = new Worker
        {
            Id = Guid.NewGuid(),
            Name = "alive-worker",
            HostName = "host2",
            Status = WorkerStatus.Active,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            Capacity = 10
        };

        await repository.RegisterAsync(deadWorker);
        await repository.RegisterAsync(aliveWorker);

        // Act
        var deadWorkers = await repository.GetDeadWorkersAsync(heartbeatTimeout);

        // Assert
        Assert.Single(deadWorkers);
        Assert.Equal(deadWorker.Id, deadWorkers[0].Id);
    }
}
