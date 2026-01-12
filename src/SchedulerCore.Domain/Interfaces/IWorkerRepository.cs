using SchedulerCore.Domain.Entities;

namespace SchedulerCore.Domain.Interfaces;

public interface IWorkerRepository
{
    Task<Worker?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Worker?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<Worker>> GetActiveWorkersAsync(CancellationToken cancellationToken = default);
    Task<Worker> RegisterAsync(Worker worker, CancellationToken cancellationToken = default);
    Task UpdateHeartbeatAsync(Guid workerId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Worker worker, CancellationToken cancellationToken = default);
    Task<List<Worker>> GetDeadWorkersAsync(TimeSpan heartbeatTimeout, CancellationToken cancellationToken = default);
}
