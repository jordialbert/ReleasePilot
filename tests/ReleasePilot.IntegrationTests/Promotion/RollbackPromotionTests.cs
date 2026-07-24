using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace ReleasePilot.IntegrationTests;

public sealed class RollbackPromotionTests : PromotionIntegrationTest
{
    private const string ApproverId = "01900000-0000-7000-8000-000000000001";
    private const string ApplicationId = "01900000-0000-7000-8000-000000000101";
    private const string DeployedVersionId = "01900000-0000-7000-8000-000000000201";
    private const string RolledBackVersionId = "01900000-0000-7000-8000-000000000202";

    [Fact]
    public async Task RollsBackDeployingPromotionExactlyOnce()
    {
        Client.DefaultRequestHeaders.Add("X-User-Id", ApproverId);
        var created = await Client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = RolledBackVersionId, targetEnvironment = "dev" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();

        var requestedRollback = await Client.PostAsync($"/promotions/{id}/rollback", null);
        Assert.Equal(HttpStatusCode.Conflict, requestedRollback.StatusCode);
        Assert.Equal(
            "invalid_promotion_transition",
            (await requestedRollback.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/approve", null)).StatusCode);
        var approvedRollback = await Client.PostAsync($"/promotions/{id}/rollback", null);
        Assert.Equal(HttpStatusCode.Conflict, approvedRollback.StatusCode);
        Assert.Equal(
            "invalid_promotion_transition",
            (await approvedRollback.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/start-deployment", null)).StatusCode);

        var responses = await Task.WhenAll(
            Client.PostAsync($"/promotions/{id}/rollback", null),
            Client.PostAsync($"/promotions/{id}/rollback", null));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        var conflict = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(
            "terminal_promotion_is_immutable",
            (await conflict.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        var details = await Client.GetFromJsonAsync<JsonElement>($"/promotions/{id}");
        Assert.Equal("rolled_back", details.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, details.GetProperty("completedAt").ValueKind);
        Assert.Equal(4, details.GetProperty("history").GetArrayLength());
        Assert.Equal(
            "promotion_rolled_back",
            details.GetProperty("history")[3].GetProperty("type").GetString());

        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*), array_agg(delivery.consumer ORDER BY delivery.consumer)
            FROM domain_events event
            JOIN event_deliveries delivery ON delivery.event_id = event.id
            WHERE event.promotion_id = $1 AND event.type = 'promotion_rolled_back'
            GROUP BY event.id
            """,
            connection);
        command.Parameters.AddWithValue(Guid.Parse(id!));
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt64(0));
        Assert.Equal(["audit", "notification"], reader.GetFieldValue<string[]>(1));
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task PreservesDeployedVersionAndRetriesRolledBackTarget()
    {
        Client.DefaultRequestHeaders.Add("X-User-Id", ApproverId);
        var deployed = await Client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = DeployedVersionId, targetEnvironment = "dev" });
        var deployedId = (await deployed.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{deployedId}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{deployedId}/start-deployment", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{deployedId}/complete", null)).StatusCode);

        var rolledBack = await Client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = RolledBackVersionId, targetEnvironment = "dev" });
        var rolledBackId = (await rolledBack.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{rolledBackId}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{rolledBackId}/start-deployment", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{rolledBackId}/rollback", null)).StatusCode);

        var status = await Client.GetFromJsonAsync<JsonElement>(
            $"/applications/{ApplicationId}/status");
        var dev = status.GetProperty("environments")[0];
        Assert.Equal(
            DeployedVersionId,
            dev.GetProperty("deployedVersion").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, dev.GetProperty("activePromotion").ValueKind);

        var retry = await Client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = RolledBackVersionId, targetEnvironment = "dev" });
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var retryId = (await retry.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Assert.NotEqual(rolledBackId, retryId);

        var history = await Client.GetFromJsonAsync<JsonElement>(
            $"/applications/{ApplicationId}/promotions");
        Assert.Equal(3, history.GetProperty("totalCount").GetInt64());
        Assert.Contains(
            history.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetString() == rolledBackId
                && item.GetProperty("status").GetString() == "rolled_back");
        Assert.Contains(
            history.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetString() == retryId
                && item.GetProperty("status").GetString() == "requested");
    }
}
