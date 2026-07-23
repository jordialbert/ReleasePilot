namespace ReleaseManagement.Domain;

public sealed class Promotion
{
    private Promotion(
        PromotionId id,
        ApplicationId applicationId,
        ApplicationVersionId applicationVersionId,
        DeploymentEnvironment targetEnvironment,
        UserId requestedBy,
        DateTimeOffset requestedAt)
    {
        Id = id;
        ApplicationId = applicationId;
        ApplicationVersionId = applicationVersionId;
        TargetEnvironment = targetEnvironment;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
        Status = PromotionStatus.Requested;
        RequestedEvent = new PromotionRequested(
            new DomainEventId(Guid.CreateVersion7()),
            id,
            requestedAt,
            requestedBy,
            applicationId,
            applicationVersionId,
            targetEnvironment);
    }

    public PromotionId Id { get; }
    public ApplicationId ApplicationId { get; }
    public ApplicationVersionId ApplicationVersionId { get; }
    public DeploymentEnvironment TargetEnvironment { get; }
    public PromotionStatus Status { get; }
    public UserId RequestedBy { get; }
    public DateTimeOffset RequestedAt { get; }
    public PromotionRequested RequestedEvent { get; }

    public static Promotion Request(
        PromotionId id,
        ApplicationVersion version,
        DeploymentEnvironment targetEnvironment,
        DeploymentEnvironment? lastCompletedEnvironment,
        bool hasActivePromotion,
        Actor actor,
        DateTimeOffset requestedAt)
    {
        if (hasActivePromotion)
        {
            throw new ActivePromotionAlreadyExists();
        }

        var expectedEnvironment = lastCompletedEnvironment switch
        {
            null => DeploymentEnvironment.Dev,
            DeploymentEnvironment.Dev => DeploymentEnvironment.Staging,
            DeploymentEnvironment.Staging => DeploymentEnvironment.Production,
            DeploymentEnvironment.Production => throw new EnvironmentAlreadyCompleted(),
            _ => throw new EnvironmentSkipped()
        };

        if (targetEnvironment != expectedEnvironment)
        {
            if (lastCompletedEnvironment is DeploymentEnvironment.Dev
                && targetEnvironment is DeploymentEnvironment.Dev)
            {
                throw new EnvironmentAlreadyCompleted();
            }

            if (lastCompletedEnvironment is DeploymentEnvironment.Staging
                && targetEnvironment is DeploymentEnvironment.Dev or DeploymentEnvironment.Staging)
            {
                throw new EnvironmentAlreadyCompleted();
            }

            throw new EnvironmentSkipped();
        }

        return new Promotion(
            id,
            version.ApplicationId,
            version.Id,
            targetEnvironment,
            actor.Id,
            requestedAt);
    }
}
