using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseManagement.Domain;

namespace ReleasePilot.IntegrationTests;

public sealed class CompletePromotionTests : PromotionIntegrationTest
{
    [Fact]
    public async Task CompletesPromotionsAndAdvancesThroughThePipeline()
    {
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");

        var initialStatus = await Client.GetFromJsonAsync<JsonElement>(
            "/applications/01900000-0000-7000-8000-000000000101/status");
        Assert.Equal(3, initialStatus.GetProperty("environments").GetArrayLength());
        Assert.Equal(
            ["dev", "staging", "production"],
            initialStatus.GetProperty("environments")
                .EnumerateArray()
                .Select(environment => environment.GetProperty("environment").GetString()));

        var created = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "dev"
            });
        var dev = await created.Content.ReadFromJsonAsync<JsonElement>();
        var devId = dev.GetProperty("id").GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{devId}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{devId}/start-deployment",
                null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{devId}/complete", null)).StatusCode);

        var devDetails = await Client.GetFromJsonAsync<JsonElement>(
            $"/promotions/{devId}");
        Assert.Equal("completed", devDetails.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, devDetails.GetProperty("completedAt").ValueKind);
        Assert.Equal(4, devDetails.GetProperty("history").GetArrayLength());
        Assert.Equal(
            "promotion_completed",
            devDetails.GetProperty("history")[3].GetProperty("type").GetString());

        var terminal = await Client.PostAsync($"/promotions/{devId}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, terminal.StatusCode);
        Assert.Equal(
            "terminal_promotion_is_immutable",
            (await terminal.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        var repeatedDev = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "dev"
            });
        Assert.Equal(HttpStatusCode.Conflict, repeatedDev.StatusCode);
        Assert.Equal(
            "environment_already_completed",
            (await repeatedDev.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        var stagingCreated = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "staging"
            });
        Assert.Equal(HttpStatusCode.Created, stagingCreated.StatusCode);
        var staging = await stagingCreated.Content.ReadFromJsonAsync<JsonElement>();
        var stagingId = staging.GetProperty("id").GetString();

        var status = await Client.GetFromJsonAsync<JsonElement>(
            "/applications/01900000-0000-7000-8000-000000000101/status");
        Assert.Equal(
            "2026.7.1",
            status.GetProperty("environments")[0]
                .GetProperty("deployedVersion")
                .GetProperty("label")
                .GetString());
        Assert.Equal(
            stagingId,
            status.GetProperty("environments")[1]
                .GetProperty("activePromotion")
                .GetProperty("id")
                .GetString());
        Assert.Equal(
            JsonValueKind.Null,
            status.GetProperty("environments")[2].GetProperty("deployedVersion").ValueKind);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{stagingId}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{stagingId}/start-deployment",
                null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{stagingId}/complete",
                null)).StatusCode);

        var productionCreated = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "production"
            });
        Assert.Equal(HttpStatusCode.Created, productionCreated.StatusCode);
        var production = await productionCreated.Content.ReadFromJsonAsync<JsonElement>();
        var productionId = production.GetProperty("id").GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{productionId}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{productionId}/start-deployment",
                null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{productionId}/complete",
                null)).StatusCode);

        var beyondProduction = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "production"
            });
        Assert.Equal(HttpStatusCode.Conflict, beyondProduction.StatusCode);
        Assert.Equal(
            "environment_already_completed",
            (await beyondProduction.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
    }

    [Fact]
    public async Task RejectsAStalePromotionSnapshotWithAControlledConflict()
    {
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");
        var created = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000203",
                targetEnvironment = "dev"
            });
        var details = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = details.GetProperty("id").GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{id}/start-deployment",
                null)).StatusCode);

        var repository = Application.Services.GetRequiredService<IPromotionRepository>();
        var promotionId = new PromotionId(Guid.Parse(id!));
        var first = (await repository.Find(promotionId, CancellationToken.None))!;
        var stale = (await repository.Find(promotionId, CancellationToken.None))!;
        var actor = new Actor(
            new UserId(Guid.Parse("01900000-0000-7000-8000-000000000001")),
            "Alex Approver",
            UserRole.Approver);
        first.Complete(actor, DateTimeOffset.UtcNow);
        stale.Complete(actor, DateTimeOffset.UtcNow);

        await repository.Update(first, CancellationToken.None);
        var conflict = await Assert.ThrowsAsync<ConcurrentPromotionUpdate>(
            () => repository.Update(stale, CancellationToken.None));

        Assert.Equal("concurrency_conflict", conflict.Code);
    }

    [Fact]
    public async Task ReturnsAControlledConflictForConcurrentCompletions()
    {
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");
        var created = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000203",
                targetEnvironment = "dev"
            });
        var details = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = details.GetProperty("id").GetString();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{id}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{id}/start-deployment",
                null)).StatusCode);

        var responses = await Task.WhenAll(
            Client.PostAsync($"/promotions/{id}/complete", null),
            Client.PostAsync($"/promotions/{id}/complete", null));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
        var conflict = Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            problem.GetProperty("code").GetString(),
            new[] { "concurrency_conflict", "terminal_promotion_is_immutable" });
    }
}
