using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class ListPromotionsQueryHandler(
    IApplicationRepository applications,
    IPromotionListReader promotions)
{
    public async Task<PromotionListPageResponse> Handle(
        ListPromotionsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1)
        {
            throw new InvalidInput("page");
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new InvalidInput("pageSize");
        }

        if (!await applications.Exists(
                new ReleaseManagement.Domain.ApplicationId(query.ApplicationId),
                cancellationToken))
        {
            throw new ResourceNotFound("application");
        }

        return await promotions.List(query, cancellationToken);
    }
}
