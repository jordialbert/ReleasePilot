namespace ReleaseManagement.Application;

public sealed record PromotionListSnapshot(
    DateTimeOffset RequestedAt,
    Guid PromotionId);
