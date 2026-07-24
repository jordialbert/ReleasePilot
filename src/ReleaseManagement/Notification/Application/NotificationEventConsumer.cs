using Microsoft.Extensions.Logging;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public sealed class NotificationEventConsumer(
    IEventDeliveryQueue deliveries,
    INotificationPort notifications,
    ILogger<NotificationEventConsumer> logger)
{
    public const string ConsumerName = "notification";

    public async Task<bool> ProcessNext(
        CancellationToken claimCancellationToken,
        CancellationToken processingCancellationToken)
    {
        var delivery = await deliveries.Claim(
            ConsumerName,
            claimCancellationToken);
        if (delivery is null)
        {
            return false;
        }

        try
        {
            await notifications.Send(
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
                processingCancellationToken);
        }
        catch (OperationCanceledException)
            when (processingCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Notification delivery {EventId} failed on attempt {Attempt}",
                delivery.EventId.Value,
                delivery.Attempt);
            if (!await deliveries.Fail(
                delivery,
                exception.Message,
                processingCancellationToken))
            {
                logger.LogWarning(
                    "Notification delivery {EventId} lost its lease before failure acknowledgement",
                    delivery.EventId.Value);
            }
            return true;
        }

        if (!await deliveries.Complete(delivery, processingCancellationToken))
        {
            logger.LogWarning(
                "Notification delivery {EventId} lost its lease before completion acknowledgement",
                delivery.EventId.Value);
        }
        return true;
    }
}
