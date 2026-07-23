namespace ReleaseManagement.Application;

public sealed record StartDeploymentCommand(
    Guid PromotionId,
    Guid ActorId);
