using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class RollbackPromotionCommandHandler(
    IUserRepository users,
    IPromotionRepository promotions)
{
    public async Task Handle(
        RollbackPromotionCommand command,
        CancellationToken cancellationToken)
    {
        var actor = await users.Find(new UserId(command.ActorId), cancellationToken)
            ?? throw new UnknownActor();
        var found = await promotions.Transition(
            new PromotionId(command.PromotionId),
            (promotion, _) =>
            {
                promotion.Rollback(actor, DateTimeOffset.UtcNow);
                return Task.CompletedTask;
            },
            cancellationToken);
        if (!found)
        {
            throw new ResourceNotFound("promotion");
        }
    }
}
