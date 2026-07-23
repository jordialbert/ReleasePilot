namespace ReleaseManagement.Domain;

public sealed record PromotionApproved(
    DomainEventId Id,
    PromotionId PromotionId,
    DateTimeOffset OccurredAt,
    UserId ActorId);
