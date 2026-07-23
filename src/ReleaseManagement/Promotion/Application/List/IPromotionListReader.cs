namespace ReleaseManagement.Application;

public interface IPromotionListReader
{
    Task<PromotionListPageResponse> List(
        ListPromotionsQuery query,
        CancellationToken cancellationToken);
}
