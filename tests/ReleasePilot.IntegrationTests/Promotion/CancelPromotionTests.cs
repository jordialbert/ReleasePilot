using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ReleaseManagement.Domain;

namespace ReleasePilot.IntegrationTests;

public sealed class CancelPromotionTests : PromotionIntegrationTest
{
    private const string ApplicationId = "01900000-0000-7000-8000-000000000101";
    private const string VersionId = "01900000-0000-7000-8000-000000000201";

    [Theory]
    [InlineData(false, 1, 2)]
    [InlineData(true, 2, 3)]
    public async Task CancelsPromotionBeforeDeployment(
        bool approved,
        int eventIndex,
        int historyLength)
    {
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000002");
        var createResponse = await Client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = VersionId, targetEnvironment = "dev" });
        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        if (approved)
        {
            Client.DefaultRequestHeaders.Remove("X-User-Id");
            Client.DefaultRequestHeaders.Add(
                "X-User-Id",
                "01900000-0000-7000-8000-000000000001");
            Assert.Equal(
                HttpStatusCode.NoContent,
                (await Client.PostAsync($"/promotions/{id}/approve", null)).StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/cancel", null)).StatusCode);

        var details = await Client.GetFromJsonAsync<JsonElement>($"/promotions/{id}");
        Assert.Equal("cancelled", details.GetProperty("status").GetString());
        Assert.Equal(
            "promotion_cancelled",
            details.GetProperty("history")[eventIndex].GetProperty("type").GetString());
        Assert.Equal(historyLength, details.GetProperty("history").GetArrayLength());
        var repeatedCancellation = await Client.PostAsync($"/promotions/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, repeatedCancellation.StatusCode);
        Assert.Equal(
            "terminal_promotion_is_immutable",
            (await repeatedCancellation.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
    }

    [Fact]
    public async Task RejectsCancellationAfterDeploymentStartsAndAfterCompletion()
    {
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000002");
        var createResponse = await Client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = VersionId, targetEnvironment = "dev" });
        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Client.DefaultRequestHeaders.Remove("X-User-Id");
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/start-deployment", null)).StatusCode);

        var deployingCancellation = await Client.PostAsync($"/promotions/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, deployingCancellation.StatusCode);
        Assert.Equal(
            "invalid_promotion_transition",
            (await deployingCancellation.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/complete", null)).StatusCode);
        var completedCancellation = await Client.PostAsync($"/promotions/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, completedCancellation.StatusCode);
        Assert.Equal(
            "terminal_promotion_is_immutable",
            (await completedCancellation.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
    }

    [Fact]
    public async Task RetriesCancelledTargetWithNewIdentityAndPreservesBothAttempts()
    {
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000002");
        var cancelledResponse = await Client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = VersionId, targetEnvironment = "dev" });
        var cancelledId = (await cancelledResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{cancelledId}/cancel", null)).StatusCode);

        var retryResponse = await Client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = VersionId, targetEnvironment = "dev" });
        var retryId = (await retryResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();

        Assert.NotEqual(cancelledId, retryId);
        var history = await Client.GetFromJsonAsync<JsonElement>(
            $"/applications/{ApplicationId}/promotions");
        Assert.Equal(2, history.GetProperty("totalCount").GetInt64());
        Assert.Contains(
            history.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetString() == cancelledId
                && item.GetProperty("status").GetString() == "cancelled");
        Assert.Contains(
            history.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetString() == retryId
                && item.GetProperty("status").GetString() == "requested");
    }

    [Fact]
    public async Task SerializesCancellationAgainstDeploymentStart()
    {
        var deployment = new BlockingDeploymentPort();
        await using var application = Application.WithWebHostBuilder(
            builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDeploymentPort>();
                services.AddSingleton<IDeploymentPort>(deployment);
            }));
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");
        var createResponse = await client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = VersionId, targetEnvironment = "dev" });
        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id")
            .GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsync($"/promotions/{id}/approve", null)).StatusCode);

        var start = client.PostAsync($"/promotions/{id}/start-deployment", null);
        await deployment.Started.Task;
        var cancel = client.PostAsync($"/promotions/{id}/cancel", null);

        var waitingForLock = false;
        while (!cancel.IsCompleted && !waitingForLock)
        {
            await using var connection = new NpgsqlConnection(Database.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_locks
                    WHERE locktype = 'advisory' AND NOT granted
                )
                """,
                connection);
            waitingForLock = (bool)(await command.ExecuteScalarAsync())!;
        }

        try
        {
            Assert.True(waitingForLock);
        }
        finally
        {
            deployment.Continue.SetResult();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await start).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cancel).StatusCode);
        var details = await client.GetFromJsonAsync<JsonElement>($"/promotions/{id}");
        Assert.Equal("deploying", details.GetProperty("status").GetString());
    }

    private sealed class BlockingDeploymentPort : IDeploymentPort
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task Start(
            PromotionId promotionId,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Continue.Task.WaitAsync(cancellationToken);
        }
    }
}
