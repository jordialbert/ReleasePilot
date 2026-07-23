namespace ReleaseManagement.Application;

public interface IPromotionHistoryReader
{
    Task<PromotionHistoryPageResponse?> List(
        Guid applicationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
