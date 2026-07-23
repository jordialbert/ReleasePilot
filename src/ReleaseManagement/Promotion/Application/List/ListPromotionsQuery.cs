namespace ReleaseManagement.Application;

public sealed record ListPromotionsQuery(
    Guid ApplicationId,
    int Page,
    int PageSize);
