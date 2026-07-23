namespace ReleaseManagement.Application;

public sealed record ApplicationStatusResponse(
    Guid Id,
    IReadOnlyList<ApplicationEnvironmentStatusResponse> Environments);
