using Microsoft.AspNetCore.Mvc;
using ReleaseManagement.Application;

namespace ReleasePilot.Api.Promotion;

[ApiController]
[Route("promotions")]
public sealed class PromotionsController(
    RequestPromotionCommandHandler requestPromotion,
    GetPromotionDetailsQueryHandler getPromotionDetails) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PromotionDetailsResponse>> RequestPromotion(
        [FromHeader(Name = "X-User-Id")] string? actorHeader,
        RequestPromotionRequest request,
        CancellationToken cancellationToken)
    {
        if (actorHeader is null)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "The X-User-Id header is required."
            };
            problem.Extensions["code"] = "missing_actor";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            return Unauthorized(problem);
        }

        if (!Guid.TryParse(actorHeader, out var actorId))
        {
            throw new InvalidInput("X-User-Id");
        }

        if (request.ApplicationVersionId == Guid.Empty)
        {
            throw new InvalidInput("applicationVersionId");
        }

        var promotion = await requestPromotion.Handle(
            new RequestPromotionCommand(
                request.ApplicationVersionId,
                request.TargetEnvironment,
                actorId),
            cancellationToken);
        return Created($"/promotions/{promotion.Id}", promotion);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PromotionDetailsResponse>> GetPromotionDetails(
        string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var promotionId))
        {
            throw new InvalidInput("id");
        }

        return await getPromotionDetails.Handle(promotionId, cancellationToken);
    }
}
