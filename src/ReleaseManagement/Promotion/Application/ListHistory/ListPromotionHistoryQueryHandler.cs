namespace ReleaseManagement.Application;

public sealed class ListPromotionHistoryQueryHandler(IPromotionHistoryReader history)
{
    public async Task<PromotionHistoryPageResponse> Handle(
        Guid applicationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new InvalidInput("page");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new InvalidInput("pageSize");
        }

        var result = await history.List(
            applicationId,
            page,
            pageSize,
            cancellationToken);
        if (result is null)
        {
            throw new ResourceNotFound("application");
        }

        return result;
    }
}
