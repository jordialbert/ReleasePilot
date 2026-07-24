using Microsoft.Extensions.Logging;

namespace ReleaseManagement.Application;

public sealed class AuditEventConsumer(
    IEventDeliveryQueue deliveries,
    IAuditLogRepository auditLog,
    TimeProvider timeProvider,
    ILogger<AuditEventConsumer> logger)
    : EventConsumer(deliveries, logger)
{
    public override string ConsumerName => EventDelivery.AuditConsumer;

    protected override Task Handle(
        EventDelivery delivery,
        CancellationToken cancellationToken) =>
        auditLog.Add(
            delivery.EventId,
            timeProvider.GetUtcNow(),
            cancellationToken);
}
