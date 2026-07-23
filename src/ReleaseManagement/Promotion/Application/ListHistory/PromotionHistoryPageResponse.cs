namespace ReleaseManagement.Application;

public sealed record PromotionHistoryPageResponse(
    IReadOnlyList<PromotionSummaryResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);
