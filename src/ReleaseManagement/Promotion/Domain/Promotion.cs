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
        UncommittedEvent = new PromotionRequested(
            new DomainEventId(Guid.CreateVersion7()),
            id,
            requestedAt,
            requestedBy,
            applicationId,
            applicationVersionId,
            targetEnvironment);
    }

    internal Promotion(
        PromotionId id,
        ApplicationId applicationId,
        ApplicationVersionId applicationVersionId,
        DeploymentEnvironment targetEnvironment,
        PromotionStatus status,
        UserId requestedBy,
        DateTimeOffset requestedAt)
    {
        Id = id;
        ApplicationId = applicationId;
        ApplicationVersionId = applicationVersionId;
        TargetEnvironment = targetEnvironment;
        Status = status;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
    }

    public PromotionId Id { get; }
    public ApplicationId ApplicationId { get; }
    public ApplicationVersionId ApplicationVersionId { get; }
    public DeploymentEnvironment TargetEnvironment { get; }
    public PromotionStatus Status { get; private set; }
    public UserId RequestedBy { get; }
    public DateTimeOffset RequestedAt { get; }
    public PromotionDomainEvent? UncommittedEvent { get; private set; }

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

    public void Approve(Actor actor, DateTimeOffset approvedAt)
    {
        if (actor.Role != UserRole.Approver)
        {
            throw new OnlyApproverCanApprove();
        }

        if (Status != PromotionStatus.Requested)
        {
            throw new InvalidPromotionTransition();
        }

        Status = PromotionStatus.Approved;
        UncommittedEvent = new PromotionApproved(
            new DomainEventId(Guid.CreateVersion7()),
            Id,
            approvedAt,
            actor.Id);
    }

    public void StartDeployment(Actor actor, DateTimeOffset startedAt)
    {
        if (Status != PromotionStatus.Approved)
        {
            throw new InvalidPromotionTransition();
        }

        Status = PromotionStatus.Deploying;
        UncommittedEvent = new DeploymentStarted(
            new DomainEventId(Guid.CreateVersion7()),
            Id,
            startedAt,
            actor.Id);
    }
}
