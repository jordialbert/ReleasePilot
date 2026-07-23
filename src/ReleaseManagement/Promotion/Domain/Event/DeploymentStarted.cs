namespace ReleaseManagement.Domain;

public sealed record DeploymentStarted(
    DomainEventId Id,
    PromotionId PromotionId,
    DateTimeOffset OccurredAt,
    UserId ActorId);
