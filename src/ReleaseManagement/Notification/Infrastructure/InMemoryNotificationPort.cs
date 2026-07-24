using System.Collections.Concurrent;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class InMemoryNotificationPort : INotificationPort
{
    private readonly ConcurrentDictionary<
        DomainEventId,
        TerminalPromotionNotification> notifications = new();

    public bool FailAfterSend { get; set; }
    public IReadOnlyDictionary<
        DomainEventId,
        TerminalPromotionNotification> Notifications => notifications;

    public Task Send(
        TerminalPromotionNotification notification,
        DomainEventId idempotencyKey,
        CancellationToken cancellationToken)
    {
        notifications.TryAdd(idempotencyKey, notification);
        if (FailAfterSend)
        {
            throw new Exception("Simulated notification failure.");
        }

        return Task.CompletedTask;
    }
}
