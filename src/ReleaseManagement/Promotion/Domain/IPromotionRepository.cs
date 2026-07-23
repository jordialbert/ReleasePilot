namespace ReleaseManagement.Domain;

public interface IPromotionRepository
{
    Task<Actor?> FindActor(UserId id, CancellationToken cancellationToken);
    Task<ApplicationVersion?> FindApplicationVersion(
        ApplicationVersionId id,
        CancellationToken cancellationToken);
    Task Add(Promotion promotion, CancellationToken cancellationToken);
}
