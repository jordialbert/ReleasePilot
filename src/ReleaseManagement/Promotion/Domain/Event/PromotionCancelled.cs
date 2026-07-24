namespace ReleaseManagement.Domain;

public sealed record PromotionCancelled(
    DomainEventId Id,
    PromotionId PromotionId,
    DateTimeOffset OccurredAt,
    UserId ActorId)
    : PromotionDomainEvent(Id, PromotionId, OccurredAt, ActorId);
