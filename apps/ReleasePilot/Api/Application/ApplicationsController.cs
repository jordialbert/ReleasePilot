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
        [FromQuery] DateTimeOffset? snapshotRequestedAt = null,
        [FromQuery] Guid? snapshotPromotionId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var applicationId))
        {
            throw new InvalidInput("id");
        }

        if (snapshotRequestedAt.HasValue != snapshotPromotionId.HasValue)
        {
            throw new InvalidInput("snapshot");
        }

        PromotionListSnapshot? snapshot = null;
        if (snapshotRequestedAt is { } requestedAt
            && snapshotPromotionId is { } promotionId)
        {
            snapshot = new PromotionListSnapshot(requestedAt, promotionId);
        }

        return await listPromotions.Handle(
            new ListPromotionsQuery(
                applicationId,
                Page: page,
                PageSize: pageSize,
                Snapshot: snapshot),
            cancellationToken);
    }
}
