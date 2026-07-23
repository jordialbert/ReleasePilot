using Microsoft.AspNetCore.Mvc;
using ReleaseManagement.Application;

namespace ReleasePilot.Api.Application;

[ApiController]
[Route("applications")]
public sealed class ApplicationsController(
    ListPromotionsQueryHandler listPromotions) : ControllerBase
{
    [HttpGet("{id}/promotions")]
    public async Task<ActionResult<PromotionListPageResponse>> ListPromotions(
        string id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var applicationId))
        {
            throw new InvalidInput("id");
        }

        return await listPromotions.Handle(
            new ListPromotionsQuery(
                applicationId,
                Page: page,
                PageSize: pageSize),
            cancellationToken);
    }
}
