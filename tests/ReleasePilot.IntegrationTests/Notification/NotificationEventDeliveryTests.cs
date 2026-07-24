using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;
using ReleaseManagement.Infrastructure;

namespace ReleasePilot.IntegrationTests;

public sealed class NotificationEventDeliveryTests : PromotionIntegrationTest
{
    private const string ApproverId = "01900000-0000-7000-8000-000000000001";
    private const string VersionId = "01900000-0000-7000-8000-000000000201";

    [Fact]
    public async Task NotifiesCompletedCancelledAndRolledBackEventsOnly()
    {
        Client.DefaultRequestHeaders.Add("X-User-Id", ApproverId);

        var completedResponse = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = VersionId,
                targetEnvironment = "dev"
            });
        Assert.Equal(HttpStatusCode.Created, completedResponse.StatusCode);
        var completedId = (await completedResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{completedId}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{completedId}/start-deployment",
                null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{completedId}/complete", null)).StatusCode);

        var cancelledResponse = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = VersionId,
                targetEnvironment = "staging"
            });
        Assert.Equal(HttpStatusCode.Created, cancelledResponse.StatusCode);
        var cancelledId = (await cancelledResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{cancelledId}/cancel", null)).StatusCode);

        var rolledBackResponse = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = VersionId,
                targetEnvironment = "staging"
            });
        Assert.Equal(HttpStatusCode.Created, rolledBackResponse.StatusCode);
        var rolledBackId = (await rolledBackResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{rolledBackId}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{rolledBackId}/start-deployment",
                null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{rolledBackId}/rollback", null)).StatusCode);

        var time = new AdjustableTimeProvider(DateTimeOffset.UtcNow.AddYears(1));
        var queue = new PostgreSqlEventDeliveryQueue(
            Database.GetConnectionString(),
            time,
            NullLogger<PostgreSqlEventDeliveryQueue>.Instance);
        var notifications = new InMemoryNotificationPort();
        var consumer = new NotificationEventConsumer(
            queue,
            notifications,
            NullLogger<NotificationEventConsumer>.Instance);

        while (await consumer.ProcessNext(
                   CancellationToken.None,
                   CancellationToken.None))
        {
        }

        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT array_agg(event.type ORDER BY event.type),
                   bool_and(delivery.status = 'completed'),
                   array_agg(event.id ORDER BY event.type)
            FROM event_deliveries delivery
            JOIN domain_events event ON event.id = delivery.event_id
            WHERE delivery.consumer = 'notification'
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(
            [
                "promotion_cancelled",
                "promotion_completed",
                "promotion_rolled_back"
            ],
            reader.GetFieldValue<string[]>(0));
        Assert.True(reader.GetBoolean(1));
        Assert.Equal(3, notifications.Notifications.Count);
        Assert.Equal(
            reader.GetFieldValue<Guid[]>(2).Order(),
            notifications.Notifications.Keys.Select(notification => notification.Value).Order());
        Assert.Equal(
            [
                new TerminalPromotionNotification(
                    new PromotionId(Guid.Parse(completedId!)),
                    PromotionStatus.Completed),
                new TerminalPromotionNotification(
                    new PromotionId(Guid.Parse(cancelledId!)),
                    PromotionStatus.Cancelled),
                new TerminalPromotionNotification(
                    new PromotionId(Guid.Parse(rolledBackId!)),
                    PromotionStatus.RolledBack)
            ],
            notifications.Notifications.Values
                .OrderBy(notification => notification.Outcome));
    }

    [Fact]
    public async Task RetriedDeliveryDoesNotDuplicateACompletedExternalEffect()
    {
        Client.DefaultRequestHeaders.Add("X-User-Id", ApproverId);
        var response = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = VersionId,
                targetEnvironment = "dev"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/start-deployment", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/complete", null)).StatusCode);

        var time = new AdjustableTimeProvider(DateTimeOffset.UtcNow.AddYears(1));
        var queue = new PostgreSqlEventDeliveryQueue(
            Database.GetConnectionString(),
            time,
            NullLogger<PostgreSqlEventDeliveryQueue>.Instance);
        var notifications = new InMemoryNotificationPort { FailAfterSend = true };
        var consumer = new NotificationEventConsumer(
            queue,
            notifications,
            NullLogger<NotificationEventConsumer>.Instance);

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));
        Assert.Single(notifications.Notifications);

        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync();
        await using (var failedCommand = new NpgsqlCommand(
            """
            SELECT status, attempts, last_error
            FROM event_deliveries
            WHERE consumer = 'notification'
            """,
            connection))
        await using (var failedReader = await failedCommand.ExecuteReaderAsync())
        {
            Assert.True(await failedReader.ReadAsync());
            Assert.Equal("pending", failedReader.GetString(0));
            Assert.Equal(1, failedReader.GetInt32(1));
            Assert.Equal("Simulated notification failure.", failedReader.GetString(2));
        }

        notifications.FailAfterSend = false;
        time.UtcNow += TimeSpan.FromSeconds(5);
        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        await using var completedCommand = new NpgsqlCommand(
            """
            SELECT status, attempts, last_error
            FROM event_deliveries
            WHERE consumer = 'notification'
            """,
            connection);
        await using var completedReader = await completedCommand.ExecuteReaderAsync();
        Assert.True(await completedReader.ReadAsync());
        Assert.Equal("completed", completedReader.GetString(0));
        Assert.Equal(2, completedReader.GetInt32(1));
        Assert.True(completedReader.IsDBNull(2));
        Assert.Single(notifications.Notifications);
    }
}
