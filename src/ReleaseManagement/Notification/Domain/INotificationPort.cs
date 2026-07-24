namespace ReleaseManagement.Domain;

public interface INotificationPort
{
    Task Send(
        TerminalPromotionNotification notification,
        DomainEventId idempotencyKey,
        CancellationToken cancellationToken);
}
