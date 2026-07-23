using System.Text.Json;

namespace ReleaseManagement.Application;

public sealed record ReleaseNotesDraftResponse(
    Guid Id,
    string Content,
    JsonElement BreakingChanges,
    DateTimeOffset CreatedAt);
