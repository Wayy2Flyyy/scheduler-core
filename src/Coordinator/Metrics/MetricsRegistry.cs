using System.Collections.Concurrent;

namespace Coordinator.Metrics;

public sealed class MetricsRegistry
{
    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.OrdinalIgnoreCase);

    public void Increment(string name, long value = 1)
    {
        _counters.AddOrUpdate(name, value, (_, current) => current + value);
    }

    public IReadOnlyDictionary<string, long> Snapshot() => new Dictionary<string, long>(_counters);
}
