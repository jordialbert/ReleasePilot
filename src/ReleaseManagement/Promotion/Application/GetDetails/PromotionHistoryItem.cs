using System.Text.Json;

namespace ReleaseManagement.Application;

public sealed record PromotionHistoryItem(
    Guid Id,
    string Type,
    DateTimeOffset OccurredAt,
    Guid ActorId,
    JsonElement Payload);
