namespace ReleaseManagement.Application;

public sealed record RequestPromotionCommand(
    Guid ApplicationVersionId,
    string TargetEnvironment,
    Guid ActorId);
