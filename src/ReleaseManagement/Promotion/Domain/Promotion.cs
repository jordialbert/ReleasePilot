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
        Actor actor,
        DateTimeOffset requestedAt)
    {
        return new Promotion(
            id,
            version.ApplicationId,
            version.Id,
            targetEnvironment,
            actor.Id,
            requestedAt);
    }
}
