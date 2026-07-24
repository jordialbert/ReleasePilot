using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed record EventDelivery(
    DomainEventId EventId,
    PromotionId PromotionId,
    string EventType,
    string Consumer,
    int Attempt,
    Guid ClaimToken)
{
    public const string AuditConsumer = "audit";
    public const string NotificationConsumer = "notification";
    public const string ReleaseNotesConsumer = "release_notes";
    public const string PromotionApprovedEventType = "promotion_approved";
}
