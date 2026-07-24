using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlEventDeliveryQueue(
    string connectionString,
    TimeProvider timeProvider,
    ILogger<PostgreSqlEventDeliveryQueue> logger)
    : IEventDeliveryQueue
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30)
    ];
    private static readonly int MaxAttempts = RetryDelays.Length + 1;

    public async Task<EventDelivery?> Claim(
        string consumer,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var claimToken = Guid.CreateVersion7();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var expiredCommand = new NpgsqlCommand(
            """
            WITH expired AS (
                SELECT event_id, consumer
                FROM event_deliveries
                WHERE consumer = $1
                  AND status = 'pending'
                  AND attempts >= $3
                  AND (locked_until IS NULL OR locked_until <= $2)
                FOR UPDATE SKIP LOCKED
            )
            UPDATE event_deliveries delivery
            SET status = 'failed',
                locked_until = NULL,
                lock_token = NULL,
                finished_at = $2,
                last_error = 'Delivery lease expired after maximum attempts.'
            FROM expired
            WHERE delivery.event_id = expired.event_id
              AND delivery.consumer = expired.consumer
            """,
            connection))
        {
            expiredCommand.Parameters.AddWithValue(consumer);
            expiredCommand.Parameters.AddWithValue(now);
            expiredCommand.Parameters.AddWithValue(MaxAttempts);
            var expired = await expiredCommand.ExecuteNonQueryAsync(cancellationToken);
            if (expired > 0)
            {
                logger.LogError(
                    "{ExpiredDeliveryCount} {Consumer} deliveries failed after their final lease expired",
                    expired,
                    consumer);
            }
        }

        await using var command = new NpgsqlCommand(
            """
            WITH candidate AS (
                SELECT delivery.event_id, delivery.consumer,
                       event.promotion_id, event.type
                FROM event_deliveries delivery
                JOIN domain_events event ON event.id = delivery.event_id
                WHERE delivery.consumer = $1
                  AND delivery.status = 'pending'
                  AND delivery.available_at <= $2
                  AND (delivery.locked_until IS NULL OR delivery.locked_until <= $2)
                  AND delivery.attempts < $5
                ORDER BY delivery.available_at, delivery.event_id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE event_deliveries delivery
            SET attempts = delivery.attempts + 1,
                locked_until = $3,
                lock_token = $4
            FROM candidate
            WHERE delivery.event_id = candidate.event_id
              AND delivery.consumer = candidate.consumer
            RETURNING delivery.event_id, delivery.consumer, delivery.attempts,
                      candidate.promotion_id, candidate.type
            """,
            connection);
        command.Parameters.AddWithValue(consumer);
        command.Parameters.AddWithValue(now);
        command.Parameters.AddWithValue(now + LeaseDuration);
        command.Parameters.AddWithValue(claimToken);
        command.Parameters.AddWithValue(MaxAttempts);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new EventDelivery(
            new DomainEventId(reader.GetGuid(0)),
            new PromotionId(reader.GetGuid(3)),
            reader.GetString(4),
            reader.GetString(1),
            reader.GetInt32(2),
            claimToken);
    }

    public async Task<bool> Complete(
        EventDelivery delivery,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            UPDATE event_deliveries
            SET status = 'completed',
                locked_until = NULL,
                lock_token = NULL,
                finished_at = $4,
                last_error = NULL
            WHERE event_id = $1
              AND consumer = $2
              AND status = 'pending'
              AND lock_token = $3
            """,
            connection);
        command.Parameters.AddWithValue(delivery.EventId.Value);
        command.Parameters.AddWithValue(delivery.Consumer);
        command.Parameters.AddWithValue(delivery.ClaimToken);
        command.Parameters.AddWithValue(timeProvider.GetUtcNow());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> Fail(
        EventDelivery delivery,
        string error,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var terminal = delivery.Attempt >= MaxAttempts;
        var status = "failed";
        object finishedAt = now;
        object retryAt = DBNull.Value;
        var retrySql = "";
        if (!terminal)
        {
            status = "pending";
            finishedAt = DBNull.Value;
            retryAt = now + RetryDelays[delivery.Attempt - 1];
            retrySql = ", available_at = $6";
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"""
            UPDATE event_deliveries
            SET status = $4{retrySql},
                locked_until = NULL,
                lock_token = NULL,
                finished_at = $5,
                last_error = $7
            WHERE event_id = $1
              AND consumer = $2
              AND status = 'pending'
              AND lock_token = $3
            """,
            connection);
        command.Parameters.AddWithValue(delivery.EventId.Value);
        command.Parameters.AddWithValue(delivery.Consumer);
        command.Parameters.AddWithValue(delivery.ClaimToken);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, finishedAt);
        command.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, retryAt);
        command.Parameters.AddWithValue(error);
        var acknowledged =
            await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        if (terminal && acknowledged)
        {
            logger.LogError(
                "Delivery {EventId} for {Consumer} failed permanently after {AttemptCount} attempts",
                delivery.EventId.Value,
                delivery.Consumer,
                delivery.Attempt);
        }

        return acknowledged;
    }
}
