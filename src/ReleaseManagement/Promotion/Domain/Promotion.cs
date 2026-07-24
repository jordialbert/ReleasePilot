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
        CommittedStatus = PromotionStatus.Requested;
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
        DateTimeOffset requestedAt,
        DateTimeOffset? completedAt)
    {
        Id = id;
        ApplicationId = applicationId;
        ApplicationVersionId = applicationVersionId;
        TargetEnvironment = targetEnvironment;
        Status = status;
        CommittedStatus = status;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
        CompletedAt = completedAt;
    }

    public PromotionId Id { get; }
    public ApplicationId ApplicationId { get; }
    public ApplicationVersionId ApplicationVersionId { get; }
    public DeploymentEnvironment TargetEnvironment { get; }
    public PromotionStatus Status { get; private set; }
    public PromotionStatus CommittedStatus { get; }
    public UserId RequestedBy { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }
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
        EnsureNotTerminal();
        if (actor.Role != UserRole.Approver)
        {
            throw new OnlyApproverCanApprove();
        }

        EnsureStatus(PromotionStatus.Requested);

        Status = PromotionStatus.Approved;
        UncommittedEvent = new PromotionApproved(
            new DomainEventId(Guid.CreateVersion7()),
            Id,
            approvedAt,
            actor.Id);
    }

    public void StartDeployment(Actor actor, DateTimeOffset startedAt)
    {
        EnsureNotTerminal();
        EnsureStatus(PromotionStatus.Approved);

        Status = PromotionStatus.Deploying;
        UncommittedEvent = new DeploymentStarted(
            new DomainEventId(Guid.CreateVersion7()),
            Id,
            startedAt,
            actor.Id);
    }

    public void Complete(Actor actor, DateTimeOffset completedAt)
    {
        EnsureNotTerminal();
        EnsureStatus(PromotionStatus.Deploying);

        Status = PromotionStatus.Completed;
        CompletedAt = completedAt;
        UncommittedEvent = new PromotionCompleted(
            new DomainEventId(Guid.CreateVersion7()),
            Id,
            completedAt,
            actor.Id);
    }

    private void EnsureNotTerminal()
    {
        if (Status.IsTerminal())
        {
            throw new TerminalPromotionIsImmutable();
        }
    }

    private void EnsureStatus(PromotionStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidPromotionTransition();
        }
    }
}
