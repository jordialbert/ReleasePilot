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

        promotion.Approve(actor, DateTimeOffset.UtcNow);
        await promotions.Update(promotion, cancellationToken);
    }
}
