using Shared;

namespace Worker.Handlers;

public interface IJobHandler
{
    string Type { get; }
    Task<JobHandlerResult> HandleAsync(JobDto job, CancellationToken cancellationToken);
}
