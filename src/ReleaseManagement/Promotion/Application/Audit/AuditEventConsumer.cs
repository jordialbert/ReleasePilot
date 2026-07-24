namespace ReleaseManagement.Application;

public sealed class AuditEventConsumer(
    IEventDeliveryQueue deliveries,
    IAuditLogRepository auditLog,
    TimeProvider timeProvider)
{
    public const string ConsumerName = "audit";

    public async Task<bool> ProcessNext(CancellationToken cancellationToken)
    {
        var delivery = await deliveries.Claim(ConsumerName, cancellationToken);
        if (delivery is null)
        {
            return false;
        }

        try
        {
            await auditLog.Add(
                delivery.EventId,
                timeProvider.GetUtcNow(),
                cancellationToken);
            await deliveries.Complete(delivery, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await deliveries.Fail(delivery, exception.ToString(), cancellationToken);
        }

        return true;
    }
}
