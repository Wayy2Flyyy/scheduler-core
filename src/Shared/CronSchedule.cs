using NCrontab;

namespace Shared;

public static class CronSchedule
{
    public static DateTimeOffset? GetNextOccurrence(string expression, DateTimeOffset? from = null)
    {
        var schedule = CrontabSchedule.Parse(expression, new CrontabSchedule.ParseOptions { IncludingSeconds = false });
        var baseTime = (from ?? DateTimeOffset.UtcNow).UtcDateTime;
        var next = schedule.GetNextOccurrence(baseTime);
        return new DateTimeOffset(next, TimeSpan.Zero);
    }
}
