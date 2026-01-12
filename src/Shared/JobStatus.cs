namespace Shared;

public enum JobStatus
{
    Pending = 0,
    Scheduled = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5,
    DeadLetter = 6
}
