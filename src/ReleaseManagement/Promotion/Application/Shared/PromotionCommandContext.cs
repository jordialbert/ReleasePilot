using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed record PromotionCommandContext(Actor Actor, Promotion Promotion)
{
    public static async Task<PromotionCommandContext> Load(
        IUserRepository users,
        IPromotionRepository promotions,
        Guid actorId,
        Guid promotionId,
        CancellationToken cancellationToken)
    {
        var actor = await users.Find(new UserId(actorId), cancellationToken)
            ?? throw new UnknownActor();
        var promotion = await promotions.Find(new PromotionId(promotionId), cancellationToken)
            ?? throw new ResourceNotFound("promotion");

        return new PromotionCommandContext(actor, promotion);
    }
}
