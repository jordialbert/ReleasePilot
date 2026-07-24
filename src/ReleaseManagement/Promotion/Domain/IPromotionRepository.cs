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
    Task<bool> Transition(
        PromotionId id,
        Func<Promotion, CancellationToken, Task> change,
        CancellationToken cancellationToken);
    Task Add(Promotion promotion, CancellationToken cancellationToken);
}
