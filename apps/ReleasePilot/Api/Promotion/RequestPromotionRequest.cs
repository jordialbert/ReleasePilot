namespace ReleasePilot.Api.Promotion;

public sealed record RequestPromotionRequest(
    Guid ApplicationVersionId,
    string TargetEnvironment);
