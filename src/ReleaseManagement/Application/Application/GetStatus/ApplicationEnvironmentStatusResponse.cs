namespace ReleaseManagement.Application;

public sealed record ApplicationEnvironmentStatusResponse(
    string Environment,
    DeployedVersionResponse? DeployedVersion,
    ActivePromotionResponse? ActivePromotion);
