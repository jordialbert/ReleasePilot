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
        Assert.Equal(promotion.Id, promotion.RequestedEvent.PromotionId);
        Assert.Equal(Actor.Id, promotion.RequestedEvent.ActorId);
        Assert.Equal(requestedAt, promotion.RequestedEvent.OccurredAt);
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

}
