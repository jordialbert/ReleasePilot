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
        var actor = await users.Find(new UserId(command.ActorId), cancellationToken)
            ?? throw new UnknownActor();
        var found = await promotions.Transition(
            new PromotionId(command.PromotionId),
            async (promotion, transitionCancellationToken) =>
            {
                promotion.StartDeployment(actor, DateTimeOffset.UtcNow);
                try
                {
                    await deployment.Start(
                        promotion.Id,
                        transitionCancellationToken);
                }
                catch (Exception) when (!transitionCancellationToken.IsCancellationRequested)
                {
                    throw new DeploymentUnavailable();
                }
            },
            cancellationToken);
        if (!found)
        {
            throw new ResourceNotFound("promotion");
        }
    }
}
