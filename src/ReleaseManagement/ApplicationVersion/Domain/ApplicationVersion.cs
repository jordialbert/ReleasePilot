namespace ReleaseManagement.Domain;

public sealed record ApplicationVersion(
    ApplicationVersionId Id,
    ApplicationId ApplicationId,
    ApplicationVersionLabel Label);
