using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ReleaseManagement.Domain;
using ReleaseManagement.Infrastructure;
using Testcontainers.PostgreSql;

namespace ReleasePilot.IntegrationTests;

public sealed class RequestPromotionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:18.4")
        .Build();
    private WebApplicationFactory<Program> application = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "001-schema.sql"))
            + await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "002-seed.sql")),
            connection);
        await command.ExecuteNonQueryAsync();

        application = new WebApplicationFactory<Program>().WithWebHostBuilder(
            builder => builder.UseSetting(
                "ConnectionStrings:PostgreSQL",
                database.GetConnectionString()));
        client = application.CreateClient();
    }

    [Fact]
    public async Task RequestsAndInspectsPromotionWithControlledConflicts()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/docs")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/docs/v1/swagger.json")).StatusCode);

        client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000002");

        var promotionRequest = new
        {
            applicationVersionId = "01900000-0000-7000-8000-000000000201",
            targetEnvironment = "dev"
        };
        var response = await client.PostAsJsonAsync("/promotions", promotionRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("requested", created.GetProperty("status").GetString());
        Assert.Equal("dev", created.GetProperty("targetEnvironment").GetString());
        Assert.Equal(
            "promotion_requested",
            created.GetProperty("history")[0].GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("releaseNotesDraft").ValueKind);

        var details = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Equal(
            created.GetProperty("id").GetString(),
            (await details.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("id")
                .GetString());

        await using (var connection = new NpgsqlConnection(database.GetConnectionString()))
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

        var activeConflict = await client.PostAsJsonAsync(
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

        var skippedEnvironment = await client.PostAsJsonAsync(
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
            client.PostAsJsonAsync("/promotions", concurrentRequest),
            client.PostAsJsonAsync("/promotions", concurrentRequest));
        Assert.Single(concurrent, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(concurrent, item => item.StatusCode == HttpStatusCode.Conflict);

        client.DefaultRequestHeaders.Remove("X-User-Id");
        var missingActor = await client.PostAsJsonAsync(
            "/promotions",
            promotionRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, missingActor.StatusCode);
        var problem = await missingActor.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("missing_actor", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));

        client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-999999999999");
        var unknownActor = await client.PostAsJsonAsync(
            "/promotions",
            promotionRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownActor.StatusCode);
        Assert.Equal(
            "unknown_actor",
            (await unknownActor.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        var missingPromotion = await client.GetAsync(
            "/promotions/01900000-0000-7000-8000-999999999999");
        Assert.Equal(HttpStatusCode.NotFound, missingPromotion.StatusCode);
    }

    [Fact]
    public async Task ApprovesRequestedPromotionWithControlledErrors()
    {
        client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");
        var created = await client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "dev"
            });
        var promotion = await created.Content.ReadFromJsonAsync<JsonElement>();
        var promotionId = promotion.GetProperty("id").GetString();

        var approval = await client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);

        Assert.Equal(HttpStatusCode.NoContent, approval.StatusCode);
        var details = await client.GetFromJsonAsync<JsonElement>(
            $"/promotions/{promotionId}");
        Assert.Equal("approved", details.GetProperty("status").GetString());
        Assert.Equal(2, details.GetProperty("history").GetArrayLength());
        Assert.Equal(
            "promotion_requested",
            details.GetProperty("history")[0].GetProperty("type").GetString());
        Assert.Equal(
            "promotion_approved",
            details.GetProperty("history")[1].GetProperty("type").GetString());

        var invalidTransition = await client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);
        Assert.Equal(HttpStatusCode.Conflict, invalidTransition.StatusCode);
        Assert.Equal(
            "invalid_promotion_transition",
            (await invalidTransition.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000002");

        var forbidden = await client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(
            "only_approver_can_approve",
            (await forbidden.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

    }

    [Fact]
    public async Task StartsDeploymentIdempotentlyAndPreservesApprovedStateOnFailure()
    {
        client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");
        var created = await client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "dev"
            });
        var promotion = await created.Content.ReadFromJsonAsync<JsonElement>();
        var promotionId = promotion.GetProperty("id").GetString();
        var approval = await client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);
        Assert.Equal(HttpStatusCode.NoContent, approval.StatusCode);

        var deployment = application.Services.GetRequiredService<InMemoryDeploymentPort>();
        deployment.Unavailable = true;

        var unavailable = await client.PostAsync(
            $"/promotions/{promotionId}/start-deployment",
            null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        var problem = await unavailable.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("deployment_unavailable", problem.GetProperty("code").GetString());
        Assert.DoesNotContain(
            "Simulated",
            problem.GetProperty("title").GetString(),
            StringComparison.OrdinalIgnoreCase);
        var approved = await client.GetFromJsonAsync<JsonElement>(
            $"/promotions/{promotionId}");
        Assert.Equal("approved", approved.GetProperty("status").GetString());
        Assert.Equal(2, approved.GetProperty("history").GetArrayLength());

        deployment.Unavailable = false;
        var started = await client.PostAsync(
            $"/promotions/{promotionId}/start-deployment",
            null);

        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
        var details = await client.GetFromJsonAsync<JsonElement>(
            $"/promotions/{promotionId}");
        Assert.Equal("deploying", details.GetProperty("status").GetString());
        Assert.Equal(3, details.GetProperty("history").GetArrayLength());
        Assert.Equal(
            "deployment_started",
            details.GetProperty("history")[2].GetProperty("type").GetString());

        await deployment.Start(
            new PromotionId(Guid.Parse(promotionId!)),
            CancellationToken.None);
        Assert.Single(deployment.Requests);

        var invalidTransition = await client.PostAsync(
            $"/promotions/{promotionId}/start-deployment",
            null);
        Assert.Equal(HttpStatusCode.Conflict, invalidTransition.StatusCode);
        Assert.Equal(
            "invalid_promotion_transition",
            (await invalidTransition.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
        Assert.Single(deployment.Requests);
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await application.DisposeAsync();
        await database.DisposeAsync();
    }
}
