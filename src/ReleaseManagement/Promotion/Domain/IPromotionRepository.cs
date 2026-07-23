namespace ReleaseManagement.Domain;

public interface IPromotionRepository
{
    Task<Actor?> FindActor(UserId id, CancellationToken cancellationToken);
    Task<ApplicationVersion?> FindApplicationVersion(
        ApplicationVersionId id,
        CancellationToken cancellationToken);
    Task<DeploymentEnvironment?> FindLastCompletedEnvironment(
        ApplicationVersionId id,
        CancellationToken cancellationToken);
    Task<bool> HasActivePromotion(
        ApplicationId applicationId,
        DeploymentEnvironment targetEnvironment,
        CancellationToken cancellationToken);
    Task<Promotion?> Find(PromotionId id, CancellationToken cancellationToken);
    Task Add(Promotion promotion, CancellationToken cancellationToken);
    Task Update(Promotion promotion, CancellationToken cancellationToken);
}
