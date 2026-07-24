using System.Text.Json;

namespace ReleaseManagement.Application;

public sealed record ModelToolCall(
    string Id,
    string Name,
    JsonElement Arguments);
