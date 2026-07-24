namespace ReleaseManagement.Application;

public sealed class AuditEventConsumer(
    IEventDeliveryQueue deliveries,
    IAuditLogRepository auditLog,
    TimeProvider timeProvider)
{
    public const string ConsumerName = "audit";

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
            await deliveries.Fail(
                delivery,
                exception.ToString(),
                processingCancellationToken);
            return true;
        }

        await deliveries.Complete(delivery, processingCancellationToken);
        return true;
    }
}
