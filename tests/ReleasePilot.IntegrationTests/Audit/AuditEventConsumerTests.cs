using ReleaseManagement.Application;
using ReleaseManagement.Domain;

namespace ReleasePilot.IntegrationTests;

public sealed class AuditEventConsumerTests
{
    [Fact]
    public async Task DoesNotFailDeliveryWhenAcknowledgementThrows()
    {
        var delivery = new EventDelivery(
            new DomainEventId(Guid.CreateVersion7()),
            AuditEventConsumer.ConsumerName,
            5,
            Guid.CreateVersion7());
        var queue = new RecordingQueue(delivery, static () => { })
        {
            CompleteException = new InvalidOperationException("PostgreSQL unavailable")
        };
        var auditLog = new RecordingAuditLog();
        var consumer = new AuditEventConsumer(
            queue,
            auditLog,
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        Assert.True(auditLog.Added);
        Assert.False(queue.Failed);
    }

    [Fact]
    public async Task ContinuesClaimedDeliveryAfterClaimCancellation()
    {
        using var claimCancellation = new CancellationTokenSource();
        using var processingCancellation = new CancellationTokenSource();
        var delivery = new EventDelivery(
            new DomainEventId(Guid.CreateVersion7()),
            AuditEventConsumer.ConsumerName,
            1,
            Guid.CreateVersion7());
        var queue = new RecordingQueue(
            delivery,
            claimCancellation.Cancel);
        var auditLog = new RecordingAuditLog();
        var consumer = new AuditEventConsumer(
            queue,
            auditLog,
            TimeProvider.System);

        Assert.True(
            await consumer.ProcessNext(
                claimCancellation.Token,
                processingCancellation.Token));

        Assert.True(claimCancellation.IsCancellationRequested);
        Assert.Equal(
            processingCancellation.Token,
            auditLog.CancellationToken);
        Assert.Equal(
            processingCancellation.Token,
            queue.CompletionCancellationToken);
    }

    private sealed class RecordingQueue(
        EventDelivery delivery,
        Action onClaim)
        : IEventDeliveryQueue
    {
        public Exception? CompleteException { get; init; }
        public bool Failed { get; private set; }
        public CancellationToken CompletionCancellationToken { get; private set; }

        public Task<EventDelivery?> Claim(
            string consumer,
            CancellationToken cancellationToken)
        {
            onClaim();
            return Task.FromResult<EventDelivery?>(delivery);
        }

        public Task<bool> Complete(
            EventDelivery claimedDelivery,
            CancellationToken cancellationToken)
        {
            CompletionCancellationToken = cancellationToken;
            if (CompleteException is not null)
            {
                return Task.FromException<bool>(CompleteException);
            }

            return Task.FromResult(true);
        }

        public Task<bool> Fail(
            EventDelivery claimedDelivery,
            string error,
            CancellationToken cancellationToken)
        {
            Failed = true;
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingAuditLog : IAuditLogRepository
    {
        public bool Added { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task Add(
            DomainEventId eventId,
            DateTimeOffset recordedAt,
            CancellationToken cancellationToken)
        {
            Added = true;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
