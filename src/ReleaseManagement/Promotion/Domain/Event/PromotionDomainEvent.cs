namespace ReleaseManagement.Domain;

public abstract record PromotionDomainEvent(
    DomainEventId Id,
    PromotionId PromotionId,
    DateTimeOffset OccurredAt,
    UserId ActorId);
