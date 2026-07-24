using Microsoft.Extensions.Logging;

namespace ReleaseManagement.Application;

public sealed class AuditEventConsumer(
    IEventDeliveryQueue deliveries,
    IAuditLogRepository auditLog,
    TimeProvider timeProvider,
    ILogger<AuditEventConsumer> logger)
{
    public const string ConsumerName = EventDelivery.AuditConsumer;

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
            await auditLog.Add(
                delivery.EventId,
                timeProvider.GetUtcNow(),
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
                "Audit delivery {EventId} failed on attempt {Attempt}",
                delivery.EventId.Value,
                delivery.Attempt);
            if (!await deliveries.Fail(
                delivery,
                exception.Message,
                processingCancellationToken))
            {
                logger.LogWarning(
                    "Audit delivery {EventId} lost its lease before failure acknowledgement",
                    delivery.EventId.Value);
            }
            return true;
        }

        if (!await deliveries.Complete(delivery, processingCancellationToken))
        {
            logger.LogWarning(
                "Audit delivery {EventId} lost its lease before completion acknowledgement",
                delivery.EventId.Value);
        }
        return true;
    }
}
