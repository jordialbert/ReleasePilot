using Microsoft.Extensions.Logging;

namespace ReleaseManagement.Application;

public sealed class ReleaseNotesEventConsumer(
    IEventDeliveryQueue deliveries,
    ReleaseNotesAgent agent,
    ILogger<ReleaseNotesEventConsumer> logger)
    : EventConsumer(deliveries, logger)
{
    public override string ConsumerName => EventDelivery.ReleaseNotesConsumer;

    protected override Task Handle(
        EventDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (delivery.EventType != EventDelivery.PromotionApprovedEventType)
        {
            throw new InvalidOperationException(
                $"Unsupported release-notes event type: {delivery.EventType}.");
        }

        return agent.Generate(
            delivery.PromotionId,
            delivery.EventId,
            cancellationToken);
    }
}
