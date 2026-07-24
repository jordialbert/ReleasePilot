using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class CancelPromotionCommandHandler(
    IUserRepository users,
    IPromotionRepository promotions)
{
    public async Task Handle(
        CancelPromotionCommand command,
        CancellationToken cancellationToken)
    {
        var actor = await users.Find(new UserId(command.ActorId), cancellationToken)
            ?? throw new UnknownActor();
        var found = await promotions.Transition(
            new PromotionId(command.PromotionId),
            (promotion, _) =>
            {
                promotion.Cancel(actor, DateTimeOffset.UtcNow);
                return Task.CompletedTask;
            },
            cancellationToken);
        if (!found)
        {
            throw new ResourceNotFound("promotion");
        }
    }
}
