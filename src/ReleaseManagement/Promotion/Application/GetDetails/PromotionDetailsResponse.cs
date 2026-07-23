namespace ReleaseManagement.Application;

public sealed record PromotionDetailsResponse(
    Guid Id,
    Guid ApplicationId,
    Guid ApplicationVersionId,
    string ApplicationVersionLabel,
    string TargetEnvironment,
    string Status,
    Guid RequestedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<PromotionHistoryItem> History,
    ReleaseNotesDraftResponse? ReleaseNotesDraft);
