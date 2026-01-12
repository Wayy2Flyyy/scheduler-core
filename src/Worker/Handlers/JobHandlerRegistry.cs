namespace Worker.Handlers;

public sealed class JobHandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IJobHandler> _handlers;

    public JobHandlerRegistry(IEnumerable<IJobHandler> handlers)
    {
        _handlers = handlers.ToDictionary(handler => handler.Type, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string type, out IJobHandler handler)
    {
        return _handlers.TryGetValue(type, out handler!);
    }
}
