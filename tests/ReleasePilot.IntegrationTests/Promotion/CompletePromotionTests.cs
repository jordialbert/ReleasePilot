using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseManagement.Domain;

namespace ReleasePilot.IntegrationTests;

public sealed class CompletePromotionTests : PromotionIntegrationTest
{
    private const string ApproverId = "01900000-0000-7000-8000-000000000001";
    private const string ApplicationId = "01900000-0000-7000-8000-000000000101";
    private const string VersionId = "01900000-0000-7000-8000-000000000201";
    private const string ConcurrencyVersionId = "01900000-0000-7000-8000-000000000203";

    [Fact]
    public async Task CompletesADeployingPromotionExactlyOnce()
    {
        UseApprover();
        var id = await StartDeploying(VersionId, "dev");

        await PostExpectingNoContent($"/promotions/{id}/complete");

        var details = await Client.GetFromJsonAsync<JsonElement>($"/promotions/{id}");
        Assert.Equal("completed", details.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, details.GetProperty("completedAt").ValueKind);
        Assert.Equal(4, details.GetProperty("history").GetArrayLength());
        Assert.Equal(
            "promotion_completed",
            details.GetProperty("history")[3].GetProperty("type").GetString());

        await AssertConflict(
            await Client.PostAsync($"/promotions/{id}/complete", null),
            "terminal_promotion_is_immutable");
    }

    [Fact]
    public async Task AdvancesOneEnvironmentAtATimeAndStopsAfterProduction()
    {
        UseApprover();
        await CompletePromotion(VersionId, "dev");

        await AssertConflict(
            await RequestPromotion(VersionId, "dev"),
            "environment_already_completed");

        await CompletePromotion(VersionId, "staging");
        await CompletePromotion(VersionId, "production");

        await AssertConflict(
            await RequestPromotion(VersionId, "production"),
            "environment_already_completed");
    }

    [Fact]
    public async Task ReportsDeployedVersionsAndActivePromotionsPerEnvironment()
    {
        UseApprover();

        var initial = await Client.GetFromJsonAsync<JsonElement>(
            $"/applications/{ApplicationId}/status");
        Assert.Equal(
            ["dev", "staging", "production"],
            initial.GetProperty("environments")
                .EnumerateArray()
                .Select(environment => environment.GetProperty("environment").GetString()));

        await CompletePromotion(VersionId, "dev");
        var stagingId = await CreatePromotion(VersionId, "staging");

        var status = await Client.GetFromJsonAsync<JsonElement>(
            $"/applications/{ApplicationId}/status");
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
    }

    [Fact]
    public async Task RejectsAStalePromotionSnapshotWithAControlledConflict()
    {
        UseApprover();
        var id = await StartDeploying(ConcurrencyVersionId, "dev");

        var repository = Application.Services.GetRequiredService<IPromotionRepository>();
        var promotionId = new PromotionId(Guid.Parse(id));
        var first = (await repository.Find(promotionId, CancellationToken.None))!;
        var stale = (await repository.Find(promotionId, CancellationToken.None))!;
        var actor = new Actor(
            new UserId(Guid.Parse(ApproverId)),
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
        UseApprover();
        var id = await StartDeploying(ConcurrencyVersionId, "dev");

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

    private void UseApprover() =>
        Client.DefaultRequestHeaders.Add("X-User-Id", ApproverId);

    private async Task<HttpResponseMessage> RequestPromotion(
        string versionId,
        string environment) =>
        await Client.PostAsJsonAsync(
            "/promotions",
            new { applicationVersionId = versionId, targetEnvironment = environment });

    private async Task<string> CreatePromotion(string versionId, string environment)
    {
        var response = await RequestPromotion(versionId, environment);
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

    private async Task<string> CompletePromotion(string versionId, string environment)
    {
        var id = await StartDeploying(versionId, environment);
        await PostExpectingNoContent($"/promotions/{id}/complete");
        return id;
    }

    private async Task PostExpectingNoContent(string path) =>
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync(path, null)).StatusCode);

    private static async Task AssertConflict(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            code,
            (await response.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
    }
}
