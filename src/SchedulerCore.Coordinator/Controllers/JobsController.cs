using Microsoft.AspNetCore.Mvc;
using SchedulerCore.Coordinator.DTOs;
using SchedulerCore.Domain.Entities;
using SchedulerCore.Domain.Interfaces;

namespace SchedulerCore.Coordinator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IJobRepository jobRepository, ILogger<JobsController> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<JobResponse>> CreateJob([FromBody] CreateJobRequest request, CancellationToken cancellationToken)
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Payload = request.Payload,
            Status = JobStatus.Pending,
            RetryCount = 0,
            MaxRetries = request.MaxRetries,
            CreatedAt = DateTime.UtcNow,
            ScheduledAt = request.ScheduledAt,
            Priority = request.Priority
        };

        await _jobRepository.CreateAsync(job, cancellationToken);
        
        _logger.LogInformation("Created job {JobId} of type {JobType}", job.Id, job.Type);

        return CreatedAtAction(nameof(GetJob), new { id = job.Id }, MapToResponse(job));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobResponse>> GetJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        if (job == null)
            return NotFound();

        return MapToResponse(job);
    }

    [HttpGet]
    public async Task<ActionResult<List<JobResponse>>> GetJobs([FromQuery] string? status, CancellationToken cancellationToken)
    {
        List<Job> jobs;
        
        if (string.IsNullOrEmpty(status))
        {
            jobs = await _jobRepository.GetPendingJobsAsync(100, cancellationToken);
        }
        else if (Enum.TryParse<JobStatus>(status, true, out var jobStatus))
        {
            jobs = await _jobRepository.GetJobsByStatusAsync(jobStatus, cancellationToken);
        }
        else
        {
            return BadRequest("Invalid status parameter");
        }

        return jobs.Select(MapToResponse).ToList();
    }

    [HttpPost("acquire")]
    public async Task<ActionResult<JobResponse>> AcquireJob([FromBody] AcquireJobRequest request, CancellationToken cancellationToken)
    {
        var leaseExpiration = DateTime.UtcNow.AddMinutes(request.LeaseDurationMinutes);
        var job = await _jobRepository.AcquireNextJobAsync(request.WorkerId, leaseExpiration, cancellationToken);
        
        if (job == null)
            return NoContent();

        _logger.LogInformation("Job {JobId} acquired by worker {WorkerId}", job.Id, request.WorkerId);
        return MapToResponse(job);
    }

    [HttpPost("{id}/complete")]
    public async Task<ActionResult> CompleteJob(Guid id, [FromBody] CompleteJobRequest request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetByIdAsync(id, cancellationToken);
        if (job == null)
            return NotFound();

        if (request.Success)
        {
            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Job {JobId} completed successfully", job.Id);
        }
        else
        {
            job.RetryCount++;
            job.LastError = request.ErrorMessage;

            if (job.RetryCount >= job.MaxRetries)
            {
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogWarning("Job {JobId} failed after {RetryCount} retries", job.Id, job.RetryCount);
            }
            else
            {
                job.Status = JobStatus.Pending;
                job.WorkerId = null;
                job.LeaseExpiresAt = null;
                job.StartedAt = null;
                _logger.LogWarning("Job {JobId} failed, retry {RetryCount}/{MaxRetries}", 
                    job.Id, job.RetryCount, job.MaxRetries);
            }
        }

        await _jobRepository.UpdateAsync(job, cancellationToken);
        return Ok();
    }

    private static JobResponse MapToResponse(Job job)
    {
        return new JobResponse
        {
            Id = job.Id,
            Name = job.Name,
            Type = job.Type,
            Payload = job.Payload,
            Status = job.Status.ToString(),
            RetryCount = job.RetryCount,
            MaxRetries = job.MaxRetries,
            CreatedAt = job.CreatedAt,
            ScheduledAt = job.ScheduledAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            WorkerId = job.WorkerId,
            LastError = job.LastError,
            Priority = job.Priority
        };
    }
}
