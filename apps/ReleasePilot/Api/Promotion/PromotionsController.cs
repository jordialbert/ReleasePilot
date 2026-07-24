using Microsoft.AspNetCore.Mvc;
using ReleaseManagement.Application;
using ReleasePilot.Api.Shared;

namespace ReleasePilot.Api.Promotion;

[ApiController]
[Route("promotions")]
public sealed class PromotionsController(
    RequestPromotionCommandHandler requestPromotion,
    ApprovePromotionCommandHandler approvePromotion,
    StartDeploymentCommandHandler startDeployment,
    CompletePromotionCommandHandler completePromotion,
    RollbackPromotionCommandHandler rollbackPromotion,
    CancelPromotionCommandHandler cancelPromotion,
    GetPromotionDetailsQueryHandler getPromotionDetails) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PromotionDetailsResponse>> RequestPromotion(
        [FromHeader(Name = "X-User-Id")] string? actorHeader,
        RequestPromotionRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = RequiredActor.Parse(actorHeader);
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

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApprovePromotion(
        string id,
        [FromHeader(Name = "X-User-Id")] string? actorHeader,
        CancellationToken cancellationToken)
    {
        var actorId = RequiredActor.Parse(actorHeader);
        var promotionId = ParsePromotionId(id);

        await approvePromotion.Handle(
            new ApprovePromotionCommand(promotionId, actorId),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/start-deployment")]
    public async Task<IActionResult> StartDeployment(
        string id,
        [FromHeader(Name = "X-User-Id")] string? actorHeader,
        CancellationToken cancellationToken)
    {
        var actorId = RequiredActor.Parse(actorHeader);
        var promotionId = ParsePromotionId(id);

        await startDeployment.Handle(
            new StartDeploymentCommand(promotionId, actorId),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompletePromotion(
        string id,
        [FromHeader(Name = "X-User-Id")] string? actorHeader,
        CancellationToken cancellationToken)
    {
        var actorId = RequiredActor.Parse(actorHeader);
        var promotionId = ParsePromotionId(id);

        await completePromotion.Handle(
            new CompletePromotionCommand(promotionId, actorId),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/rollback")]
    public async Task<IActionResult> RollbackPromotion(
        string id,
        [FromHeader(Name = "X-User-Id")] string? actorHeader,
        CancellationToken cancellationToken)
    {
        var actorId = RequiredActor.Parse(actorHeader);
        var promotionId = ParsePromotionId(id);

        await rollbackPromotion.Handle(
            new RollbackPromotionCommand(promotionId, actorId),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelPromotion(
        string id,
        [FromHeader(Name = "X-User-Id")] string? actorHeader,
        CancellationToken cancellationToken)
    {
        var actorId = RequiredActor.Parse(actorHeader);
        var promotionId = ParsePromotionId(id);

        await cancelPromotion.Handle(
            new CancelPromotionCommand(promotionId, actorId),
            cancellationToken);
        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PromotionDetailsResponse>> GetPromotionDetails(
        string id,
        CancellationToken cancellationToken)
    {
        return await getPromotionDetails.Handle(ParsePromotionId(id), cancellationToken);
    }

    private static Guid ParsePromotionId(string id)
    {
        if (!Guid.TryParse(id, out var promotionId))
        {
            throw new InvalidInput("id");
        }

        return promotionId;
    }
}
