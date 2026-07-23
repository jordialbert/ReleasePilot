namespace ReleaseManagement.Domain;

public interface IDeploymentPort
{
    Task Start(PromotionId promotionId, CancellationToken cancellationToken);
}
