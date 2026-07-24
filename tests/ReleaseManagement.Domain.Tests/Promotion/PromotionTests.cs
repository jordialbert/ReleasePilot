using ReleaseManagement.Domain;

namespace ReleaseManagement.Domain.Tests;

public sealed class PromotionTests
{
    private static readonly ApplicationVersion Version = new(
        new ApplicationVersionId(Guid.Parse("01900000-0000-7000-8000-000000000201")),
        new ApplicationId(Guid.Parse("01900000-0000-7000-8000-000000000101")),
        new ApplicationVersionLabel("2026.7.1"));

    private static readonly Actor Actor = new(
        new UserId(Guid.Parse("01900000-0000-7000-8000-000000000002")),
        "Riley Operator",
        UserRole.Operator);

    private static readonly Actor Approver = new(
        new UserId(Guid.Parse("01900000-0000-7000-8000-000000000001")),
        "Alex Approver",
        UserRole.Approver);

    [Fact]
    public void RequestsFirstDevPromotionAndRecordsItsEvent()
    {
        var requestedAt = DateTimeOffset.Parse("2026-07-23T12:00:00Z");

        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Actor,
            requestedAt);

        Assert.Equal(PromotionStatus.Requested, promotion.Status);
        Assert.Equal(DeploymentEnvironment.Dev, promotion.TargetEnvironment);
        var domainEvent = Assert.IsType<PromotionRequested>(promotion.UncommittedEvent);
        Assert.Equal(promotion.Id, domainEvent.PromotionId);
        Assert.Equal(Actor.Id, domainEvent.ActorId);
        Assert.Equal(requestedAt, domainEvent.OccurredAt);
    }

    [Theory]
    [InlineData(DeploymentEnvironment.Staging)]
    [InlineData(DeploymentEnvironment.Production)]
    public void RejectsAFirstPromotionOutsideDev(DeploymentEnvironment target)
    {
        var exception = Assert.Throws<EnvironmentSkipped>(() => Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            target,
            null,
            false,
            Actor,
            DateTimeOffset.UtcNow));

        Assert.Equal("environment_skipped", exception.Code);
    }

    [Fact]
    public void RejectsACompetingActivePromotion()
    {
        var exception = Assert.Throws<ActivePromotionAlreadyExists>(() => Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            true,
            Actor,
            DateTimeOffset.UtcNow));

        Assert.Equal("active_promotion_already_exists", exception.Code);
    }

    [Fact]
    public void ApproverApprovesRequestedPromotionAndRecordsItsEvent()
    {
        var approvedAt = DateTimeOffset.Parse("2026-07-23T12:05:00Z");
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Approver,
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));

        promotion.Approve(Approver, approvedAt);

        Assert.Equal(PromotionStatus.Approved, promotion.Status);
        var domainEvent = Assert.IsType<PromotionApproved>(promotion.UncommittedEvent);
        Assert.Equal(promotion.Id, domainEvent.PromotionId);
        Assert.Equal(Approver.Id, domainEvent.ActorId);
        Assert.Equal(approvedAt, domainEvent.OccurredAt);
    }

    [Fact]
    public void OperatorCannotApprovePromotion()
    {
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Actor,
            DateTimeOffset.UtcNow);
        var uncommittedEvent = promotion.UncommittedEvent;

        var exception = Assert.Throws<OnlyApproverCanApprove>(
            () => promotion.Approve(Actor, DateTimeOffset.UtcNow));

        Assert.Equal("only_approver_can_approve", exception.Code);
        Assert.Equal(PromotionStatus.Requested, promotion.Status);
        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);
    }

    [Fact]
    public void ApprovedPromotionCannotBeApprovedAgain()
    {
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Approver,
            DateTimeOffset.UtcNow);
        promotion.Approve(Approver, DateTimeOffset.UtcNow);
        var uncommittedEvent = promotion.UncommittedEvent;

        var exception = Assert.Throws<InvalidPromotionTransition>(
            () => promotion.Approve(Approver, DateTimeOffset.UtcNow));

        Assert.Equal("invalid_promotion_transition", exception.Code);
        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);
    }

    [Fact]
    public void StartsApprovedPromotionAndRecordsItsEvent()
    {
        var startedAt = DateTimeOffset.Parse("2026-07-23T12:10:00Z");
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Approver,
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
        promotion.Approve(Approver, DateTimeOffset.Parse("2026-07-23T12:05:00Z"));

        promotion.StartDeployment(Actor, startedAt);

        Assert.Equal(PromotionStatus.Deploying, promotion.Status);
        var domainEvent = Assert.IsType<DeploymentStarted>(promotion.UncommittedEvent);
        Assert.Equal(promotion.Id, domainEvent.PromotionId);
        Assert.Equal(Actor.Id, domainEvent.ActorId);
        Assert.Equal(startedAt, domainEvent.OccurredAt);
    }

    [Fact]
    public void RequestedPromotionCannotStartDeployment()
    {
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Actor,
            DateTimeOffset.UtcNow);
        var uncommittedEvent = promotion.UncommittedEvent;

        var exception = Assert.Throws<InvalidPromotionTransition>(
            () => promotion.StartDeployment(Actor, DateTimeOffset.UtcNow));

        Assert.Equal("invalid_promotion_transition", exception.Code);
        Assert.Equal(PromotionStatus.Requested, promotion.Status);
        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);
    }

    [Fact]
    public void CompletesDeployingPromotionAndRecordsItsEvent()
    {
        var completedAt = DateTimeOffset.Parse("2026-07-23T12:15:00Z");
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Approver,
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
        promotion.Approve(Approver, DateTimeOffset.Parse("2026-07-23T12:05:00Z"));
        promotion.StartDeployment(Actor, DateTimeOffset.Parse("2026-07-23T12:10:00Z"));

        promotion.Complete(Actor, completedAt);

        Assert.Equal(PromotionStatus.Completed, promotion.Status);
        Assert.Equal(completedAt, promotion.CompletedAt);
        var domainEvent = Assert.IsType<PromotionCompleted>(promotion.UncommittedEvent);
        Assert.Equal(promotion.Id, domainEvent.PromotionId);
        Assert.Equal(Actor.Id, domainEvent.ActorId);
        Assert.Equal(completedAt, domainEvent.OccurredAt);
    }

    [Fact]
    public void CompletedPromotionRejectsEveryLaterTransition()
    {
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Approver,
            DateTimeOffset.UtcNow);
        promotion.Approve(Approver, DateTimeOffset.UtcNow);
        promotion.StartDeployment(Actor, DateTimeOffset.UtcNow);
        promotion.Complete(Actor, DateTimeOffset.UtcNow);
        var uncommittedEvent = promotion.UncommittedEvent;

        Assert.Throws<TerminalPromotionIsImmutable>(
            () => promotion.Approve(Approver, DateTimeOffset.UtcNow));
        Assert.Throws<TerminalPromotionIsImmutable>(
            () => promotion.StartDeployment(Actor, DateTimeOffset.UtcNow));
        Assert.Throws<TerminalPromotionIsImmutable>(
            () => promotion.Complete(Actor, DateTimeOffset.UtcNow));
        Assert.Throws<TerminalPromotionIsImmutable>(
            () => promotion.Cancel(Actor, DateTimeOffset.UtcNow));
        Assert.Equal(PromotionStatus.Completed, promotion.Status);
        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelsPromotionBeforeDeploymentAndRecordsItsEvent(bool approved)
    {
        var cancelledAt = DateTimeOffset.Parse("2026-07-23T12:10:00Z");
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Actor,
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"));
        if (approved)
        {
            promotion.Approve(Approver, DateTimeOffset.Parse("2026-07-23T12:05:00Z"));
        }

        promotion.Cancel(Actor, cancelledAt);

        Assert.Equal(PromotionStatus.Cancelled, promotion.Status);
        var domainEvent = Assert.IsType<PromotionCancelled>(promotion.UncommittedEvent);
        Assert.Equal(promotion.Id, domainEvent.PromotionId);
        Assert.Equal(Actor.Id, domainEvent.ActorId);
        Assert.Equal(cancelledAt, domainEvent.OccurredAt);
    }

    [Fact]
    public void DeployingPromotionCannotBeCancelled()
    {
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Actor,
            DateTimeOffset.UtcNow);
        promotion.Approve(Approver, DateTimeOffset.UtcNow);
        promotion.StartDeployment(Actor, DateTimeOffset.UtcNow);
        var uncommittedEvent = promotion.UncommittedEvent;

        Assert.Throws<InvalidPromotionTransition>(
            () => promotion.Cancel(Actor, DateTimeOffset.UtcNow));
        Assert.Equal(PromotionStatus.Deploying, promotion.Status);
        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);
    }

    [Fact]
    public void CancelledPromotionRejectsEveryLaterTransition()
    {
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Dev,
            null,
            false,
            Actor,
            DateTimeOffset.UtcNow);
        promotion.Cancel(Actor, DateTimeOffset.UtcNow);
        var uncommittedEvent = promotion.UncommittedEvent;

        Assert.Throws<TerminalPromotionIsImmutable>(
            () => promotion.Approve(Approver, DateTimeOffset.UtcNow));
        Assert.Throws<TerminalPromotionIsImmutable>(
            () => promotion.StartDeployment(Actor, DateTimeOffset.UtcNow));
        Assert.Throws<TerminalPromotionIsImmutable>(
            () => promotion.Complete(Actor, DateTimeOffset.UtcNow));
        Assert.Throws<TerminalPromotionIsImmutable>(
            () => promotion.Cancel(Actor, DateTimeOffset.UtcNow));
        Assert.Equal(PromotionStatus.Cancelled, promotion.Status);
        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);
    }

    [Theory]
    [InlineData(DeploymentEnvironment.Dev, DeploymentEnvironment.Staging)]
    [InlineData(DeploymentEnvironment.Staging, DeploymentEnvironment.Production)]
    public void RequestsOnlyTheEnvironmentAfterThePipelinePosition(
        DeploymentEnvironment completed,
        DeploymentEnvironment target)
    {
        var promotion = Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            target,
            completed,
            false,
            Actor,
            DateTimeOffset.UtcNow);

        Assert.Equal(target, promotion.TargetEnvironment);
    }

    [Fact]
    public void ProductionPipelinePositionEndsProgression()
    {
        Assert.Throws<EnvironmentAlreadyCompleted>(() => Promotion.Request(
            new PromotionId(Guid.CreateVersion7()),
            Version,
            DeploymentEnvironment.Production,
            DeploymentEnvironment.Production,
            false,
            Actor,
            DateTimeOffset.UtcNow));
    }

}
