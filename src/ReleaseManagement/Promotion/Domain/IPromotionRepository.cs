namespace ReleaseManagement.Domain;

public interface IPromotionRepository
{
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
    Task StartDeployment(Promotion promotion, CancellationToken cancellationToken);
}
