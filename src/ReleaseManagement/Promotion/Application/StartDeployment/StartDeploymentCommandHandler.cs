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
        var actor = await users.Find(
            new UserId(command.ActorId),
            cancellationToken);
        if (actor is null)
        {
            throw new UnknownActor();
        }

        var promotion = await promotions.Find(
            new PromotionId(command.PromotionId),
            cancellationToken);
        if (promotion is null)
        {
            throw new ResourceNotFound("promotion");
        }

        promotion.StartDeployment(actor, DateTimeOffset.UtcNow);
        try
        {
            await deployment.Start(promotion.Id, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DeploymentUnavailable();
        }

        await promotions.StartDeployment(promotion, cancellationToken);
    }
}
