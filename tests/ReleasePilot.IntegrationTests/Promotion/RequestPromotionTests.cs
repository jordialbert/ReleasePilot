using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace ReleasePilot.IntegrationTests;

public sealed class RequestPromotionTests : PromotionIntegrationTest
{
    [Fact]
    public async Task RequestsAndInspectsPromotionWithControlledConflicts()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/docs")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await Client.GetAsync("/docs/v1/swagger.json")).StatusCode);

        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000002");

        var promotionRequest = new
        {
            applicationVersionId = "01900000-0000-7000-8000-000000000201",
            targetEnvironment = "dev"
        };
        var response = await Client.PostAsJsonAsync("/promotions", promotionRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("requested", created.GetProperty("status").GetString());
        Assert.Equal("dev", created.GetProperty("targetEnvironment").GetString());
        Assert.Equal(
            "promotion_requested",
            created.GetProperty("history")[0].GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("releaseNotesDraft").ValueKind);

        var details = await Client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Equal(
            created.GetProperty("id").GetString(),
            (await details.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id")
                .GetString());

        await using (var connection = new NpgsqlConnection(Database.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT
                    (SELECT count(*) FROM promotions),
                    (SELECT count(*) FROM domain_events)
                """,
                connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1, reader.GetInt64(0));
            Assert.Equal(1, reader.GetInt64(1));
        }

        var activeConflict = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000202",
                targetEnvironment = "dev"
            });
        Assert.Equal(HttpStatusCode.Conflict, activeConflict.StatusCode);
        Assert.Equal(
            "active_promotion_already_exists",
            (await activeConflict.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        var skippedEnvironment = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000203",
                targetEnvironment = "production"
            });
        Assert.Equal(HttpStatusCode.Conflict, skippedEnvironment.StatusCode);
        Assert.Equal(
            "environment_skipped",
            (await skippedEnvironment.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        var concurrentRequest = new
        {
            applicationVersionId = "01900000-0000-7000-8000-000000000203",
            targetEnvironment = "dev"
        };
        var concurrent = await Task.WhenAll(
            Client.PostAsJsonAsync("/promotions", concurrentRequest),
            Client.PostAsJsonAsync("/promotions", concurrentRequest));
        Assert.Single(concurrent, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(concurrent, item => item.StatusCode == HttpStatusCode.Conflict);

        Client.DefaultRequestHeaders.Remove("X-User-Id");
        var missingActor = await Client.PostAsJsonAsync(
            "/promotions",
            promotionRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, missingActor.StatusCode);
        var problem = await missingActor.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("missing_actor", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));

        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-999999999999");
        var unknownActor = await Client.PostAsJsonAsync(
            "/promotions",
            promotionRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownActor.StatusCode);
        Assert.Equal(
            "unknown_actor",
            (await unknownActor.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        var missingPromotion = await Client.GetAsync(
            "/promotions/01900000-0000-7000-8000-999999999999");
        Assert.Equal(HttpStatusCode.NotFound, missingPromotion.StatusCode);
    }
}
