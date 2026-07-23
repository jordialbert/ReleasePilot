namespace ReleaseManagement.Application;

public interface IPromotionDetailsReader
{
    Task<PromotionDetailsResponse?> Find(Guid id, CancellationToken cancellationToken);
}
