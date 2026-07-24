namespace ReleaseManagement.Application;

public interface IEventDeliveryQueue
{
    Task<EventDelivery?> Claim(
        string consumer,
        CancellationToken cancellationToken);

    // False means this claim no longer owns the delivery.
    Task<bool> Complete(
        EventDelivery delivery,
        CancellationToken cancellationToken);

    // False means this claim no longer owns the delivery.
    Task<bool> Fail(
        EventDelivery delivery,
        string error,
        CancellationToken cancellationToken);
}
