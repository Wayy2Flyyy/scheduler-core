using Microsoft.AspNetCore.Mvc;
using SchedulerCore.Coordinator.DTOs;
using SchedulerCore.Domain.Interfaces;

namespace SchedulerCore.Coordinator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkersController : ControllerBase
{
    private readonly IWorkerRepository _workerRepository;
    private readonly ILogger<WorkersController> _logger;

    public WorkersController(IWorkerRepository workerRepository, ILogger<WorkersController> logger)
    {
        _workerRepository = workerRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkerStatusResponse>>> GetWorkers(CancellationToken cancellationToken)
    {
        var workers = await _workerRepository.GetActiveWorkersAsync(cancellationToken);
        
        return workers.Select(w => new WorkerStatusResponse
        {
            Id = w.Id,
            Name = w.Name,
            HostName = w.HostName,
            Status = w.Status.ToString(),
            RegisteredAt = w.RegisteredAt,
            LastHeartbeat = w.LastHeartbeat,
            Capacity = w.Capacity,
            ActiveJobs = w.ActiveJobs
        }).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkerStatusResponse>> GetWorker(Guid id, CancellationToken cancellationToken)
    {
        var worker = await _workerRepository.GetByIdAsync(id, cancellationToken);
        if (worker == null)
            return NotFound();

        return new WorkerStatusResponse
        {
            Id = worker.Id,
            Name = worker.Name,
            HostName = worker.HostName,
            Status = worker.Status.ToString(),
            RegisteredAt = worker.RegisteredAt,
            LastHeartbeat = worker.LastHeartbeat,
            Capacity = worker.Capacity,
            ActiveJobs = worker.ActiveJobs
        };
    }
}
