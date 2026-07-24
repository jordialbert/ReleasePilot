namespace ReleaseManagement.Domain;

public sealed record PromotionRolledBack(
    DomainEventId Id,
    PromotionId PromotionId,
    DateTimeOffset OccurredAt,
    UserId ActorId)
    : PromotionDomainEvent(Id, PromotionId, OccurredAt, ActorId);
