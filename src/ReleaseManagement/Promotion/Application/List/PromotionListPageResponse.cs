namespace ReleaseManagement.Application;

public sealed record PromotionListPageResponse(
    IReadOnlyList<PromotionSummaryResponse> Items,
    int Page,
    int PageSize,
    long TotalCount,
    PromotionListSnapshot? Snapshot);
