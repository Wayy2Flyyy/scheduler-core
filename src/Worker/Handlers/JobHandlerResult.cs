using System.Text.Json;

namespace Worker.Handlers;

public sealed record JobHandlerResult(bool Success, string? Error, JsonElement? Result);
