namespace ReleaseManagement.Application;

public sealed record DeployedVersionResponse(
    Guid Id,
    string Label,
    DateTimeOffset CompletedAt);
