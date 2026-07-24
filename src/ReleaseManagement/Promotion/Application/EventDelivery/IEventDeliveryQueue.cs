namespace ReleaseManagement.Application;

public interface IEventDeliveryQueue
{
    Task<EventDelivery?> Claim(
        string consumer,
        CancellationToken cancellationToken);

    Task<bool> Complete(
        EventDelivery delivery,
        CancellationToken cancellationToken);

    Task<bool> Fail(
        EventDelivery delivery,
        string error,
        CancellationToken cancellationToken);
}
