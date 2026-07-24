using Microsoft.Extensions.Logging;

namespace ReleaseManagement.Application;

public abstract class EventConsumer(
    IEventDeliveryQueue deliveries,
    ILogger logger)
{
    public abstract string ConsumerName { get; }

    protected abstract Task Handle(
        EventDelivery delivery,
        CancellationToken cancellationToken);

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
            await Handle(delivery, processingCancellationToken);
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
                "{Consumer} delivery {EventId} failed on attempt {Attempt}",
                ConsumerName,
                delivery.EventId.Value,
                delivery.Attempt);
            if (!await deliveries.Fail(
                delivery,
                exception.Message,
                processingCancellationToken))
            {
                logger.LogWarning(
                    "{Consumer} delivery {EventId} lost its lease before failure acknowledgement",
                    ConsumerName,
                    delivery.EventId.Value);
            }
            return true;
        }

        if (!await deliveries.Complete(delivery, processingCancellationToken))
        {
            logger.LogWarning(
                "{Consumer} delivery {EventId} lost its lease before completion acknowledgement",
                ConsumerName,
                delivery.EventId.Value);
            return true;
        }

        logger.LogInformation(
            "{Consumer} delivery {EventId} for Promotion {PromotionId} ({EventType}) completed on attempt {Attempt}",
            ConsumerName,
            delivery.EventId.Value,
            delivery.PromotionId.Value,
            delivery.EventType,
            delivery.Attempt);
        return true;
    }
}
