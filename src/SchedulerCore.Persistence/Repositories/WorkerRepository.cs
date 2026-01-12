using Microsoft.EntityFrameworkCore;
using SchedulerCore.Domain.Entities;
using SchedulerCore.Domain.Interfaces;

namespace SchedulerCore.Persistence.Repositories;

public class WorkerRepository : IWorkerRepository
{
    private readonly SchedulerDbContext _context;

    public WorkerRepository(SchedulerDbContext context)
    {
        _context = context;
    }

    public async Task<Worker?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Workers.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Worker?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Workers
            .FirstOrDefaultAsync(w => w.Name == name, cancellationToken);
    }

    public async Task<List<Worker>> GetActiveWorkersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Workers
            .Where(w => w.Status == WorkerStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<Worker> RegisterAsync(Worker worker, CancellationToken cancellationToken = default)
    {
        _context.Workers.Add(worker);
        await _context.SaveChangesAsync(cancellationToken);
        return worker;
    }

    public async Task UpdateHeartbeatAsync(Guid workerId, CancellationToken cancellationToken = default)
    {
        var worker = await GetByIdAsync(workerId, cancellationToken);
        if (worker != null)
        {
            worker.LastHeartbeat = DateTime.UtcNow;
            worker.Status = WorkerStatus.Active;
            await UpdateAsync(worker, cancellationToken);
        }
    }

    public async Task UpdateAsync(Worker worker, CancellationToken cancellationToken = default)
    {
        _context.Workers.Update(worker);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Worker>> GetDeadWorkersAsync(TimeSpan heartbeatTimeout, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - heartbeatTimeout;
        return await _context.Workers
            .Where(w => w.Status == WorkerStatus.Active && w.LastHeartbeat < cutoff)
            .ToListAsync(cancellationToken);
    }
}
