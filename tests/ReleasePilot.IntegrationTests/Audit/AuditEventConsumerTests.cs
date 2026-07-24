using Microsoft.Extensions.Logging;
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
            TimeProvider.System,
            queue);

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
            TimeProvider.System,
            queue);

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

    [Fact]
    public async Task LogsFailuresAndLostFailureAcknowledgements()
    {
        var exception = new InvalidOperationException("Audit insert failed");
        var delivery = new EventDelivery(
            new DomainEventId(Guid.CreateVersion7()),
            AuditEventConsumer.ConsumerName,
            5,
            Guid.CreateVersion7());
        var queue = new RecordingQueue(delivery, static () => { })
        {
            FailResult = false
        };
        var consumer = new AuditEventConsumer(
            queue,
            new RecordingAuditLog { Exception = exception },
            TimeProvider.System,
            queue);

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        Assert.Equal(exception.Message, queue.FailureError);
        Assert.Collection(
            queue.Logs,
            log =>
            {
                Assert.Equal(LogLevel.Error, log.Level);
                Assert.Same(exception, log.Exception);
            },
            log => Assert.Equal(LogLevel.Warning, log.Level));
    }

    [Fact]
    public async Task LogsLostCompletionAcknowledgements()
    {
        var delivery = new EventDelivery(
            new DomainEventId(Guid.CreateVersion7()),
            AuditEventConsumer.ConsumerName,
            5,
            Guid.CreateVersion7());
        var queue = new RecordingQueue(delivery, static () => { })
        {
            CompleteResult = false
        };
        var consumer = new AuditEventConsumer(
            queue,
            new RecordingAuditLog(),
            TimeProvider.System,
            queue);

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        var log = Assert.Single(queue.Logs);
        Assert.Equal(LogLevel.Warning, log.Level);
    }

    private sealed class RecordingQueue(
        EventDelivery delivery,
        Action onClaim)
        : IEventDeliveryQueue, ILogger<AuditEventConsumer>
    {
        public Exception? CompleteException { get; init; }
        public bool CompleteResult { get; init; } = true;
        public bool FailResult { get; init; } = true;
        public bool Failed { get; private set; }
        public string? FailureError { get; private set; }
        public List<(LogLevel Level, Exception? Exception)> Logs { get; } = [];
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

            return Task.FromResult(CompleteResult);
        }

        public Task<bool> Fail(
            EventDelivery claimedDelivery,
            string error,
            CancellationToken cancellationToken)
        {
            Failed = true;
            FailureError = error;
            return Task.FromResult(FailResult);
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Logs.Add((logLevel, exception));
    }

    private sealed class RecordingAuditLog : IAuditLogRepository
    {
        public Exception? Exception { get; init; }
        public bool Added { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task Add(
            DomainEventId eventId,
            DateTimeOffset recordedAt,
            CancellationToken cancellationToken)
        {
            if (Exception is not null)
            {
                return Task.FromException(Exception);
            }

            Added = true;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
