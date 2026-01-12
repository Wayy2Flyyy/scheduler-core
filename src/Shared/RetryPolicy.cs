namespace Shared;

public static class RetryPolicy
{
    public static DateTimeOffset CalculateNextRun(
        DateTimeOffset now,
        int attempt,
        int baseDelaySeconds = 5,
        int maxDelaySeconds = 300)
    {
        if (attempt < 1)
        {
            attempt = 1;
        }

        var exponential = Math.Pow(2, attempt - 1);
        var delay = Math.Min(maxDelaySeconds, baseDelaySeconds * exponential);
        var jitter = Random.Shared.NextDouble() * 0.2 + 0.9;
        var totalSeconds = delay * jitter;
        return now.AddSeconds(totalSeconds);
    }
}
