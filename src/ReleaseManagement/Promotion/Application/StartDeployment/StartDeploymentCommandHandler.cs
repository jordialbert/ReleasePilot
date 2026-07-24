using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class StartDeploymentCommandHandler(
    IUserRepository users,
    IPromotionRepository promotions,
    IDeploymentPort deployment)
{
    public async Task Handle(
        StartDeploymentCommand command,
        CancellationToken cancellationToken)
    {
        var (actor, promotion) = await PromotionCommandContext.Load(
            users,
            promotions,
            command.ActorId,
            command.PromotionId,
            cancellationToken);

        promotion.StartDeployment(actor, DateTimeOffset.UtcNow);
        try
        {
            await deployment.Start(promotion.Id, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DeploymentUnavailable();
        }

        await promotions.Update(promotion, cancellationToken);
    }
}
