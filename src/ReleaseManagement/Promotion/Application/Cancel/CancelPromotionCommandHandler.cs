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
        await using var transitionLock = await promotions.AcquireTransitionLock(
            new PromotionId(command.PromotionId),
            cancellationToken);
        var (actor, promotion) = await PromotionCommandContext.Load(
            users,
            promotions,
            command.ActorId,
            command.PromotionId,
            cancellationToken);

        promotion.Cancel(actor, DateTimeOffset.UtcNow);
        await promotions.Update(promotion, cancellationToken);
    }
}
