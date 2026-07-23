using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class RequestPromotionCommandHandler(
    IPromotionRepository promotions,
    IPromotionDetailsReader details)
{
    public async Task<PromotionDetailsResponse> Handle(
        RequestPromotionCommand command,
        CancellationToken cancellationToken)
    {
        var targetEnvironment = command.TargetEnvironment switch
        {
            "dev" => DeploymentEnvironment.Dev,
            "staging" => DeploymentEnvironment.Staging,
            "production" => DeploymentEnvironment.Production,
            _ => throw new InvalidInput("targetEnvironment")
        };

        var actor = await promotions.FindActor(
            new UserId(command.ActorId),
            cancellationToken);
        if (actor is null)
        {
            throw new UnknownActor();
        }

        var versionId = new ApplicationVersionId(command.ApplicationVersionId);
        var version = await promotions.FindApplicationVersion(versionId, cancellationToken);
        if (version is null)
        {
            throw new ResourceNotFound("application_version");
        }

        var lastCompletedEnvironment = await promotions.FindLastCompletedEnvironment(
            versionId,
            cancellationToken);
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            version,
            targetEnvironment,
            lastCompletedEnvironment,
            actor,
            DateTimeOffset.UtcNow);

        await promotions.Add(promotion, cancellationToken);

        return (await details.Find(promotion.Id.Value, cancellationToken))!;
    }
}
