namespace ReleaseManagement.Domain;

public sealed record PromotionRequested(
    DomainEventId Id,
    PromotionId PromotionId,
    DateTimeOffset OccurredAt,
    UserId ActorId,
    ApplicationId ApplicationId,
    ApplicationVersionId ApplicationVersionId,
    DeploymentEnvironment TargetEnvironment);
