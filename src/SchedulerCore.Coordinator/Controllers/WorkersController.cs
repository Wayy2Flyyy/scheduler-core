using Microsoft.AspNetCore.Mvc;
using SchedulerCore.Coordinator.DTOs;
using SchedulerCore.Domain.Entities;
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

    [HttpPost("register")]
    public async Task<ActionResult<WorkerStatusResponse>> RegisterWorker([FromBody] RegisterWorkerRequest request, CancellationToken cancellationToken)
    {
        // Check if worker already exists
        var existingWorker = await _workerRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existingWorker != null)
        {
            // Update existing worker
            existingWorker.LastHeartbeat = DateTime.UtcNow;
            existingWorker.Status = WorkerStatus.Active;
            await _workerRepository.UpdateAsync(existingWorker, cancellationToken);
            
            _logger.LogInformation("Worker {WorkerName} re-registered", request.Name);
            
            return new WorkerStatusResponse
            {
                Id = existingWorker.Id,
                Name = existingWorker.Name,
                HostName = existingWorker.HostName,
                Status = existingWorker.Status.ToString(),
                RegisteredAt = existingWorker.RegisteredAt,
                LastHeartbeat = existingWorker.LastHeartbeat,
                Capacity = existingWorker.Capacity,
                ActiveJobs = existingWorker.ActiveJobs
            };
        }

        // Register new worker
        var worker = new Worker
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            HostName = request.HostName,
            Status = WorkerStatus.Active,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            Capacity = request.Capacity,
            ActiveJobs = 0
        };

        await _workerRepository.RegisterAsync(worker, cancellationToken);
        _logger.LogInformation("Worker {WorkerName} ({WorkerId}) registered", worker.Name, worker.Id);

        return CreatedAtAction(nameof(GetWorker), new { id = worker.Id }, new WorkerStatusResponse
        {
            Id = worker.Id,
            Name = worker.Name,
            HostName = worker.HostName,
            Status = worker.Status.ToString(),
            RegisteredAt = worker.RegisteredAt,
            LastHeartbeat = worker.LastHeartbeat,
            Capacity = worker.Capacity,
            ActiveJobs = worker.ActiveJobs
        });
    }

    [HttpPost("{id}/heartbeat")]
    public async Task<ActionResult> Heartbeat(Guid id, CancellationToken cancellationToken)
    {
        await _workerRepository.UpdateHeartbeatAsync(id, cancellationToken);
        _logger.LogDebug("Heartbeat received from worker {WorkerId}", id);
        return Ok();
    }
}
