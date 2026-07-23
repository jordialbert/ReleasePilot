namespace ReleaseManagement.Application;

public sealed class GetPromotionDetailsQueryHandler(IPromotionDetailsReader details)
{
    public async Task<PromotionDetailsResponse> Handle(
        Guid id,
        CancellationToken cancellationToken)
    {
        var promotion = await details.Find(id, cancellationToken);
        if (promotion is null)
        {
            throw new ResourceNotFound("promotion");
        }

        return promotion;
    }
}
