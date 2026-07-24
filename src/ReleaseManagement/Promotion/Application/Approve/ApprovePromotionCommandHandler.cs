using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class ApprovePromotionCommandHandler(
    IUserRepository users,
    IPromotionRepository promotions)
{
    public async Task Handle(
        ApprovePromotionCommand command,
        CancellationToken cancellationToken)
    {
        var (actor, promotion) = await PromotionCommandContext.Load(
            users,
            promotions,
            command.ActorId,
            command.PromotionId,
            cancellationToken);

        promotion.Approve(actor, DateTimeOffset.UtcNow);
        await promotions.Update(promotion, cancellationToken);
    }
}
