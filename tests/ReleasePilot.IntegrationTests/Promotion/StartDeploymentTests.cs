using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ReleaseManagement.Domain;
using ReleaseManagement.Infrastructure;

namespace ReleasePilot.IntegrationTests;

public sealed class StartDeploymentTests : PromotionIntegrationTest
{
    [Fact]
    public async Task StartsDeploymentIdempotentlyAndPreservesApprovedStateOnFailure()
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
        var promotion = await created.Content.ReadFromJsonAsync<JsonElement>();
        var promotionId = promotion.GetProperty("id").GetString();
        var approval = await Client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);
        Assert.Equal(HttpStatusCode.NoContent, approval.StatusCode);

        var deployment = Application.Services.GetRequiredService<InMemoryDeploymentPort>();
        deployment.Unavailable = true;

        var unavailable = await Client.PostAsync(
            $"/promotions/{promotionId}/start-deployment",
            null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        var problem = await unavailable.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("deployment_unavailable", problem.GetProperty("code").GetString());
        Assert.DoesNotContain(
            "Simulated",
            problem.GetProperty("title").GetString(),
            StringComparison.OrdinalIgnoreCase);
        var approved = await Client.GetFromJsonAsync<JsonElement>(
            $"/promotions/{promotionId}");
        Assert.Equal("approved", approved.GetProperty("status").GetString());
        Assert.Equal(2, approved.GetProperty("history").GetArrayLength());

        deployment.Unavailable = false;
        var started = await Client.PostAsync(
            $"/promotions/{promotionId}/start-deployment",
            null);

        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
        var details = await Client.GetFromJsonAsync<JsonElement>(
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

        var invalidTransition = await Client.PostAsync(
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
}
