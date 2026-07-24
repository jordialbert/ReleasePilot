using Microsoft.Extensions.Logging;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class NotificationEventConsumer(
    IEventDeliveryQueue deliveries,
    INotificationPort notifications,
    ILogger<NotificationEventConsumer> logger)
    : EventConsumer(deliveries, logger)
{
    public override string ConsumerName => EventDelivery.NotificationConsumer;

    protected override Task Handle(
        EventDelivery delivery,
        CancellationToken cancellationToken) =>
        notifications.Send(
            new TerminalPromotionNotification(
                delivery.PromotionId,
                delivery.EventType switch
                {
                    "promotion_completed" => PromotionStatus.Completed,
                    "promotion_cancelled" => PromotionStatus.Cancelled,
                    "promotion_rolled_back" => PromotionStatus.RolledBack,
                    _ => throw new InvalidOperationException(
                        $"Unsupported notification event type: {delivery.EventType}.")
                }),
            delivery.EventId,
            cancellationToken);
}
