using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class CompletePromotionCommandHandler(
    IUserRepository users,
    IPromotionRepository promotions)
{
    public async Task Handle(
        CompletePromotionCommand command,
        CancellationToken cancellationToken)
    {
        var (actor, promotion) = await PromotionCommandContext.Load(
            users,
            promotions,
            command.ActorId,
            command.PromotionId,
            cancellationToken);

        promotion.Complete(actor, DateTimeOffset.UtcNow);
        await promotions.Update(promotion, cancellationToken);
    }
}
