namespace ReleaseManagement.Application;

public sealed record ActivePromotionResponse(
    Guid Id,
    Guid ApplicationVersionId,
    string ApplicationVersionLabel,
    string Status);
