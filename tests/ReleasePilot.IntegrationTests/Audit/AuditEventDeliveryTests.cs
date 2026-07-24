using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;
using ReleaseManagement.Infrastructure;

namespace ReleasePilot.IntegrationTests;

public sealed class AuditEventDeliveryTests : PromotionIntegrationTest
{
    private const string ApproverId = "01900000-0000-7000-8000-000000000001";
    private const string VersionId = "01900000-0000-7000-8000-000000000201";
    private static readonly DateTimeOffset AuditTime =
        DateTimeOffset.UtcNow.AddYears(1);

    [Fact]
    public async Task ClaimsConcurrentlyAndRejectsStaleTokensAfterLeaseExpiry()
    {
        UseApprover();
        await CreatePromotion(VersionId, "dev");
        var time = new AdjustableTimeProvider(AuditTime);
        var queue = new PostgreSqlEventDeliveryQueue(
            Database.GetConnectionString(),
            time,
            NullLogger<PostgreSqlEventDeliveryQueue>.Instance);

        var claims = await Task.WhenAll(
            queue.Claim(AuditEventConsumer.ConsumerName, CancellationToken.None),
            queue.Claim(AuditEventConsumer.ConsumerName, CancellationToken.None));

        var first = Assert.Single(claims, claim => claim is not null)!;
        Assert.Single(claims, claim => claim is null);
        Assert.Null(
            await queue.Claim(
                AuditEventConsumer.ConsumerName,
                CancellationToken.None));

        time.UtcNow += TimeSpan.FromSeconds(60);
        var second = Assert.IsType<EventDelivery>(
            await queue.Claim(
                AuditEventConsumer.ConsumerName,
                CancellationToken.None));
        Assert.Equal(2, second.Attempt);
        Assert.NotEqual(first.ClaimToken, second.ClaimToken);
        Assert.False(
            await queue.Complete(first, CancellationToken.None));
        Assert.False(
            await queue.Fail(
                first,
                "stale",
                CancellationToken.None));
        Assert.True(
            await queue.Complete(second, CancellationToken.None));

        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            """
            SELECT status, attempts, locked_until, lock_token, finished_at
            FROM event_deliveries
            WHERE event_id = $1 AND consumer = $2
            """,
            connection);
        command.Parameters.AddWithValue(second.EventId.Value);
        command.Parameters.AddWithValue(AuditEventConsumer.ConsumerName);
        await using var reader =
            await command.ExecuteReaderAsync(CancellationToken.None);
        Assert.True(await reader.ReadAsync(CancellationToken.None));
        Assert.Equal("completed", reader.GetString(0));
        Assert.Equal(2, reader.GetInt32(1));
        Assert.True(reader.IsDBNull(2));
        Assert.True(reader.IsDBNull(3));
        Assert.Equal(time.UtcNow, reader.GetFieldValue<DateTimeOffset>(4));
    }

    [Fact]
    public async Task RetriesAtConfiguredTimesAndFailsTheFifthAttempt()
    {
        UseApprover();
        await CreatePromotion(VersionId, "dev");
        var time = new AdjustableTimeProvider(AuditTime);
        var queue = new PostgreSqlEventDeliveryQueue(
            Database.GetConnectionString(),
            time,
            NullLogger<PostgreSqlEventDeliveryQueue>.Instance);
        int[] retryDelays = [5, 10, 20, 30];

        for (var index = 0; index < retryDelays.Length; index++)
        {
            var delivery = Assert.IsType<EventDelivery>(
                await queue.Claim(
                    AuditEventConsumer.ConsumerName,
                    CancellationToken.None));
            Assert.Equal(index + 1, delivery.Attempt);
            Assert.True(
                await queue.Fail(
                    delivery,
                    $"attempt {delivery.Attempt}",
                    CancellationToken.None));
            Assert.Null(
                await queue.Claim(
                    AuditEventConsumer.ConsumerName,
                    CancellationToken.None));
            time.UtcNow += TimeSpan.FromSeconds(retryDelays[index]);
        }

        var final = Assert.IsType<EventDelivery>(
            await queue.Claim(
                AuditEventConsumer.ConsumerName,
                CancellationToken.None));
        Assert.Equal(5, final.Attempt);
        Assert.True(
            await queue.Fail(
                final,
                "terminal",
                CancellationToken.None));

        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            """
            SELECT status, attempts, locked_until, lock_token,
                   finished_at, last_error
            FROM event_deliveries
            WHERE event_id = $1 AND consumer = $2
            """,
            connection);
        command.Parameters.AddWithValue(final.EventId.Value);
        command.Parameters.AddWithValue(AuditEventConsumer.ConsumerName);
        await using var reader =
            await command.ExecuteReaderAsync(CancellationToken.None);
        Assert.True(await reader.ReadAsync(CancellationToken.None));
        Assert.Equal("failed", reader.GetString(0));
        Assert.Equal(5, reader.GetInt32(1));
        Assert.True(reader.IsDBNull(2));
        Assert.True(reader.IsDBNull(3));
        Assert.Equal(time.UtcNow, reader.GetFieldValue<DateTimeOffset>(4));
        Assert.Equal("terminal", reader.GetString(5));
    }

    [Fact]
    public async Task FailsAnExpiredFifthClaimWithoutClaimingAgain()
    {
        UseApprover();
        await CreatePromotion(VersionId, "dev");
        var time = new AdjustableTimeProvider(AuditTime);
        var queue = new PostgreSqlEventDeliveryQueue(
            Database.GetConnectionString(),
            time,
            NullLogger<PostgreSqlEventDeliveryQueue>.Instance);
        int[] retryDelays = [5, 10, 20, 30];

        foreach (var retryDelay in retryDelays)
        {
            var delivery = Assert.IsType<EventDelivery>(
                await queue.Claim(
                    AuditEventConsumer.ConsumerName,
                    CancellationToken.None));
            Assert.True(
                await queue.Fail(
                    delivery,
                    $"attempt {delivery.Attempt}",
                    CancellationToken.None));
            time.UtcNow += TimeSpan.FromSeconds(retryDelay);
        }

        var fifth = Assert.IsType<EventDelivery>(
            await queue.Claim(
                AuditEventConsumer.ConsumerName,
                CancellationToken.None));
        Assert.Equal(5, fifth.Attempt);
        time.UtcNow += TimeSpan.FromSeconds(60);

        Assert.Null(
            await queue.Claim(
                AuditEventConsumer.ConsumerName,
                CancellationToken.None));

        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            """
            SELECT status, attempts, finished_at, last_error
            FROM event_deliveries
            WHERE event_id = $1 AND consumer = $2
            """,
            connection);
        command.Parameters.AddWithValue(fifth.EventId.Value);
        command.Parameters.AddWithValue(AuditEventConsumer.ConsumerName);
        await using var reader =
            await command.ExecuteReaderAsync(CancellationToken.None);
        Assert.True(await reader.ReadAsync(CancellationToken.None));
        Assert.Equal("failed", reader.GetString(0));
        Assert.Equal(5, reader.GetInt32(1));
        Assert.Equal(time.UtcNow, reader.GetFieldValue<DateTimeOffset>(2));
        Assert.Equal(
            "Delivery lease expired after maximum attempts.",
            reader.GetString(3));
    }

    [Fact]
    public async Task AuditsEveryEventAndCompletesEachDelivery()
    {
        UseApprover();
        await CompletePromotion(VersionId, "dev");
        var cancelledId = await CreatePromotion(VersionId, "staging");
        await PostExpectingNoContent($"/promotions/{cancelledId}/cancel");
        var rolledBackId = await StartDeploying(VersionId, "staging");
        await PostExpectingNoContent($"/promotions/{rolledBackId}/rollback");
        var time = new AdjustableTimeProvider(AuditTime);
        var queue = new PostgreSqlEventDeliveryQueue(
            Database.GetConnectionString(),
            time,
            NullLogger<PostgreSqlEventDeliveryQueue>.Instance);
        var auditLog =
            new PostgreSqlAuditLogRepository(Database.GetConnectionString());
        var consumer = new AuditEventConsumer(
            queue,
            auditLog,
            time,
            NullLogger<AuditEventConsumer>.Instance);

        while (await consumer.ProcessNext(
                   CancellationToken.None,
                   CancellationToken.None))
        {
        }

        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                (SELECT count(*) FROM domain_events),
                (SELECT count(*) FROM audit_log),
                (SELECT count(*)
                 FROM event_deliveries
                 WHERE consumer = $1 AND status = 'completed'),
                NOT EXISTS (
                    SELECT 1
                    FROM domain_events event
                    LEFT JOIN audit_log audit ON audit.event_id = event.id
                    WHERE audit.type IS DISTINCT FROM event.type
                       OR audit.promotion_id IS DISTINCT FROM event.promotion_id
                       OR audit.occurred_at IS DISTINCT FROM event.occurred_at
                       OR audit.actor_id IS DISTINCT FROM event.actor_id
                ),
                bool_and(recorded_at = $2),
                array_agg(DISTINCT type ORDER BY type)
            FROM audit_log
            """,
            connection);
        command.Parameters.AddWithValue(AuditEventConsumer.ConsumerName);
        command.Parameters.AddWithValue(AuditTime);
        await using var reader =
            await command.ExecuteReaderAsync(CancellationToken.None);
        Assert.True(await reader.ReadAsync(CancellationToken.None));
        var eventCount = reader.GetInt64(0);
        var auditCount = reader.GetInt64(1);
        var completedCount = reader.GetInt64(2);
        var metadataMatches = reader.GetBoolean(3);
        var recordedAtMatches = reader.GetBoolean(4);
        var auditedTypes = reader.GetFieldValue<string[]>(5);

        Assert.Equal(10, eventCount);
        Assert.Equal(eventCount, auditCount);
        Assert.Equal(eventCount, completedCount);
        Assert.True(metadataMatches);
        Assert.True(recordedAtMatches);
        Assert.Equal(
            [
                "deployment_started",
                "promotion_approved",
                "promotion_cancelled",
                "promotion_completed",
                "promotion_requested",
                "promotion_rolled_back"
            ],
            auditedTypes);
    }

    [Fact]
    public async Task DoesNotDuplicateAuditEntriesByEventId()
    {
        UseApprover();
        await CreatePromotion(VersionId, "dev");
        var time = new AdjustableTimeProvider(AuditTime);
        var queue = new PostgreSqlEventDeliveryQueue(
            Database.GetConnectionString(),
            time,
            NullLogger<PostgreSqlEventDeliveryQueue>.Instance);
        var auditLog =
            new PostgreSqlAuditLogRepository(Database.GetConnectionString());
        var consumer = new AuditEventConsumer(
            queue,
            auditLog,
            time,
            NullLogger<AuditEventConsumer>.Instance);
        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync(CancellationToken.None);
        Guid eventId;
        await using (var command = new NpgsqlCommand(
            "SELECT event_id FROM audit_log",
            connection))
        {
            eventId = (Guid)(await command.ExecuteScalarAsync(
                CancellationToken.None))!;
        }

        time.UtcNow += TimeSpan.FromHours(1);
        await auditLog.Add(
            new DomainEventId(eventId),
            time.UtcNow,
            CancellationToken.None);

        await using var verification = new NpgsqlCommand(
            """
            SELECT count(*), min(recorded_at), max(recorded_at)
            FROM audit_log
            WHERE event_id = $1
            """,
            connection);
        verification.Parameters.AddWithValue(eventId);
        await using var reader =
            await verification.ExecuteReaderAsync(CancellationToken.None);
        Assert.True(await reader.ReadAsync(CancellationToken.None));
        var count = reader.GetInt64(0);
        var earliestRecordedAt = reader.GetFieldValue<DateTimeOffset>(1);
        var latestRecordedAt = reader.GetFieldValue<DateTimeOffset>(2);

        Assert.Equal(1, count);
        Assert.Equal(AuditTime, earliestRecordedAt);
        Assert.Equal(AuditTime, latestRecordedAt);
    }

    private void UseApprover() =>
        Client.DefaultRequestHeaders.Add("X-User-Id", ApproverId);

    private async Task<string> CreatePromotion(string versionId, string environment)
    {
        var response = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = versionId,
                targetEnvironment = environment
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString()!;
    }

    private async Task<string> StartDeploying(string versionId, string environment)
    {
        var id = await CreatePromotion(versionId, environment);
        await PostExpectingNoContent($"/promotions/{id}/approve");
        await PostExpectingNoContent($"/promotions/{id}/start-deployment");
        return id;
    }

    private async Task CompletePromotion(string versionId, string environment)
    {
        var id = await StartDeploying(versionId, environment);
        await PostExpectingNoContent($"/promotions/{id}/complete");
    }

    private async Task PostExpectingNoContent(string path) =>
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(path, null)).StatusCode);
}
