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
