using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;
using ReleaseManagement.Infrastructure;

namespace ReleasePilot.IntegrationTests;

public sealed class ReleaseNotesEventDeliveryTests : PromotionIntegrationTest
{
    [Fact]
    public async Task PersistsOneDraftAndExposesItThroughPromotionDetails()
    {
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");
        var created = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "dev"
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var promotionId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetGuid();

        var approval = await Client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);

        Assert.Equal(HttpStatusCode.NoContent, approval.StatusCode);
        var processingDetails = await Client.GetFromJsonAsync<JsonElement>(
            $"/promotions/{promotionId}");
        Assert.Equal(
            JsonValueKind.Null,
            processingDetails.GetProperty("releaseNotesDraft").ValueKind);

        var time = new AdjustableTimeProvider(DateTimeOffset.UtcNow.AddYears(1));
        var queue = new PostgreSqlEventDeliveryQueue(
            Database.GetConnectionString(),
            time,
            NullLogger<PostgreSqlEventDeliveryQueue>.Instance);
        var agent = new ReleaseNotesAgent(
            new DeterministicLanguageModel(),
            new InMemoryIssueTrackerPort(),
            new PostgreSqlReleaseNotesDraftRepository(
                Database.GetConnectionString(),
                time));
        var consumer = new ReleaseNotesEventConsumer(
            queue,
            agent,
            NullLogger<ReleaseNotesEventConsumer>.Instance);

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        var completedDetails = await Client.GetFromJsonAsync<JsonElement>(
            $"/promotions/{promotionId}");
        var draft = completedDetails.GetProperty("releaseNotesDraft");
        Assert.Contains("RP-101", draft.GetProperty("content").GetString());
        Assert.Equal(
            "RP-102",
            draft.GetProperty("breakingChanges")[0]
                .GetProperty("workItemId")
                .GetString());
        Assert.Equal(
            "Removes the v1 authentication endpoint.",
            draft.GetProperty("breakingChanges")[0]
                .GetProperty("reason")
                .GetString());

        await using var connection =
            new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync();
        await using (var replayCommand = new NpgsqlCommand(
            """
            UPDATE event_deliveries
            SET status = 'pending',
                attempts = 0,
                available_at = $1,
                locked_until = NULL,
                lock_token = NULL,
                finished_at = NULL
            WHERE consumer = 'release_notes'
            """,
            connection))
        {
            replayCommand.Parameters.AddWithValue(time.GetUtcNow());
            Assert.Equal(1, await replayCommand.ExecuteNonQueryAsync());
        }

        Assert.True(
            await consumer.ProcessNext(
                CancellationToken.None,
                CancellationToken.None));

        await using var countCommand = new NpgsqlCommand(
            "SELECT count(*) FROM release_notes_drafts",
            connection);
        Assert.Equal(1L, await countCommand.ExecuteScalarAsync());
    }
}
