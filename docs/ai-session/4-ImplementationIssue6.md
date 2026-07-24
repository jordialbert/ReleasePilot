# Implement issue 6

> Implement issue 6 under .scratch

<details><summary>7 previous messages</summary>

> I’ll inspect `.scratch` for issue 6, trace the relevant code and tests, then implement the smallest focused change and run only the affected verification.
>
> Issue 6 fits the existing command/aggregate/persistence pattern cleanly. I’m checking the SQL status/event mappings and the active-promotion queries now, because cancellation must both persist atomically and release the retry slot without special-case compatibility code.
>
> <details><summary>Created 4 files, edited 6 files</summary>
>
> - Wrote `./apps/ReleasePilot/Api/Program.cs` (+1 -0)
> - Wrote `./apps/ReleasePilot/Api/Promotion/PromotionsController.cs` (+16 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Application/Cancel/CancelPromotionCommand.cs` (+5 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Application/Cancel/CancelPromotionCommandHandler.cs` (+23 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/Event/PromotionCancelled.cs` (+8 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/Promotion.cs` (+16 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionEventWriter.cs` (+4 -0)
> - Wrote `./tests/ReleaseManagement.Domain.Tests/Promotion/PromotionTests.cs` (+77 -0)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+121 -0)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+18 -10)
>
> </details>
>
> The domain behavior is in place and its focused suite passes (18 tests). I’ve kept cancellation to the two allowed source states, with terminal-state guards taking precedence; next I’m running the new API/integration tests against PostgreSQL to verify the atomic event write and retry history end to end.
>
> <details><summary>Edited a file</summary>
>
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+66 -40)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+7 -23)
>
> </details>
>
> The PostgreSQL-backed API tests now pass (4 cases, including both source states, retry with a new identity, preserved list history, invalid late cancellation, and immutability). I’m doing the final diff audit now for scope, project conventions, and accidental unrelated changes.
>
> <details><summary>Edited a file</summary>
>
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+0 -1)
>
> </details>
</details>

Implemented issue 6.

- Added `POST /promotions/{id}/cancel`.
- Requested and Approved promotions can be cancelled.
- Added atomic `PromotionCancelled` event persistence.
- Cancelled promotions are terminal and immutable.
- Deploying/Completed cancellation is rejected.
- Cancellation releases the target for retry with a new promotion identity.
- Canonical history preserves cancelled and retry attempts.

Verification passed:

- Domain tests: 18 passed.
- Cancellation integration tests: 4 passed.

> create the new branch, make atomic commits for the changes, open a new ready for review PR

<details><summary>4 previous messages</summary>

> I’m using the GitHub publishing skill for the branch/commit/PR workflow. I’ll first verify the current branch, remote, and exact diff, then split the implementation into the smallest sensible atomic commits and open the PR ready for review.
>
> The worktree contains only the issue 6 implementation, GitHub authentication is valid, and `main` matches the expected starting point. I’m creating the issue’s specified `feat/cancel-promotion` branch and splitting the work into four dependency-ordered commits: domain behavior, persistence, API exposure, and end-to-end coverage.
>
> All four commits are created and the branch is clean. I’m re-running the two focused suites from the committed state; once they pass, I’ll push the branch and open the ready-for-review PR with a concise issue-focused description.
>
> <details><summary>Called a tool</summary>
>
> MCP tool call
>
> codex_apps.github.create_pull_request
>
> ```json
> {
>   "repository_full_name": "jordialbert/ReleasePilot",
>   "base": "main",
>   "head": "feat/cancel-promotion",
>   "title": "feat: cancel promotions before deployment",
>   "body": "## Summary\n\n- allow Requested and Approved promotions to be cancelled through `POST /promotions/{id}/cancel`\n- persist one `PromotionCancelled` domain event with the terminal snapshot\n- release cancelled targets for retry with a new promotion identity while preserving both attempts in history\n- reject cancellation after deployment begins and keep cancelled promotions immutable\n\n## Why\n\nRelease managers need to stop work before deployment starts without rewriting history or blocking a later retry of the same environment.\n\n## Validation\n\n- `dotnet test tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj --no-restore`\n- `dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CancelPromotionTests`",
>   "draft": false,
>   "maintainer_can_modify": true
> }
> ```
>
> ```text
> Action completed.
> ```
>
> ```json
> {
>   "url": "https://github.com/jordialbert/ReleasePilot/pull/9",
>   "number": 9,
>   "state": "open",
>   "merged": false,
>   "mergeable": false,
>   "draft": false,
>   "body": "## Summary\n\n- allow Requested and Approved promotions to be cancelled through `POST /promotions/{id}/cancel`\n- persist one `PromotionCancelled` domain event with the terminal snapshot\n- release cancelled targets for retry with a new promotion identity while preserving both attempts in history\n- reject cancellation after deployment begins and keep cancelled promotions immutable\n\n## Why\n\nRelease managers need to stop work before deployment starts without rewriting history or blocking a later retry of the same environment.\n\n## Validation\n\n- `dotnet test tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj --no-restore`\n- `dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CancelPromotionTests`",
>   "title": "feat: cancel promotions before deployment",
>   "base": "main",
>   "base_sha": "88ec9d8e0f0d9c26704d11cdd6b7da0159152cbc",
>   "head": "feat/cancel-promotion",
>   "head_sha": "7e5568542864d31dc67eba21acd89849b4104a0b",
>   "merge_commit_sha": null,
>   "user": {
>     "login": "jordialbert",
>     "name": "jordialbert",
>     "email": null,
>     "avatar_url": "https://avatars.githubusercontent.com/u/63541019?v=4",
>     "id": 63541019
>   },
>   "requested_reviewers": null,
>   "requested_team_reviewers": null,
>   "diff": null,
>   "comments": null,
>   "created_at": "2026-07-24T00:32:54Z",
>   "updated_at": "2026-07-24T00:32:54Z",
>   "closed_at": null,
>   "merged_at": null,
>   "commits": 4,
>   "changed_files": 9,
>   "additions": 288,
>   "deletions": 0,
>   "display_url": "https://github.com/jordialbert/ReleasePilot/pull/9",
>   "display_title": "feat: cancel promotions before deployment"
> }
> ```
>
> </details>
</details>

Created and published:

- Branch: `feat/cancel-promotion`
- Four atomic commits covering domain behavior, persistence, API, and tests
- Focused tests passed: 18 domain, 4 integration
- Ready PR: [#9 — feat: cancel promotions before deployment](https://github.com/jordialbert/ReleasePilot/pull/9)
- PR is open, non-draft, and mergeable.

> fetch the PR comments, analyze them and address them if they make sense. Check the challenge and the plan https://backend-challenge-2026.theagilemonkeys.com

<details><summary>10 previous messages</summary>

> I’m using the GitHub review-feedback skill to fetch and triage every PR comment, and I’ll cross-check each suggestion against the local `PLAN.md`, `SPEC.md`, issue 6, and the published challenge before changing anything. I’ll only implement comments that are correct for the stated challenge and keep the diff minimal.
>
> <details><summary>Called a tool, searched the web</summary>
>
> MCP tool call
>
> codex_apps.github.fetch_pr
>
> ```json
> {
>   "repo_full_name": "jordialbert/ReleasePilot",
>   "pr_number": 9
> }
> ```
>
> ```text
> Action completed.
> ```
>
> ```json
> {
>   "pull_request": {
>     "url": "https://github.com/jordialbert/ReleasePilot/pull/9",
>     "number": 9,
>     "state": "open",
>     "merged": false,
>     "mergeable": true,
>     "draft": false,
>     "body": "## Summary\n\n- allow Requested and Approved promotions to be cancelled through `POST /promotions/{id}/cancel`\n- persist one `PromotionCancelled` domain event with the terminal snapshot\n- release cancelled targets for retry with a new promotion identity while preserving both attempts in history\n- reject cancellation after deployment begins and keep cancelled promotions immutable\n\n## Why\n\nRelease managers need to stop work before deployment starts without rewriting history or blocking a later retry of the same environment.\n\n## Validation\n\n- `dotnet test tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj --no-restore`\n- `dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CancelPromotionTests`",
>     "title": "feat: cancel promotions before deployment",
>     "base": "main",
>     "base_sha": "88ec9d8e0f0d9c26704d11cdd6b7da0159152cbc",
>     "head": "feat/cancel-promotion",
>     "head_sha": "7e5568542864d31dc67eba21acd89849b4104a0b",
>     "merge_commit_sha": "b65f727787b4863f0f5aac958fd4283773869017",
>     "user": {
>       "login": "jordialbert",
>       "name": "jordialbert",
>       "email": null,
>       "avatar_url": "https://avatars.githubusercontent.com/u/63541019?v=4",
>       "id": 63541019
>     },
>     "requested_reviewers": null,
>     "requested_team_reviewers": null,
>     "diff": "@@ -53,6 +53,7 @@\n builder.Services.AddScoped<ApprovePromotionCommandHandler>();\n builder.Services.AddScoped<StartDeploymentCommandHandler>();\n builder.Services.AddScoped<CompletePromotionCommandHandler>();\n+builder.Services.AddScoped<CancelPromotionCommandHandler>();\n builder.Services.AddScoped<GetPromotionDetailsQueryHandler>();\n builder.Services.AddScoped<ListPromotionsQueryHandler>();\n builder.Services.AddScoped<GetApplicationStatusQueryHandler>();\n@@ -11,6 +11,7 @@ public sealed class PromotionsController(\n     ApprovePromotionCommandHandler approvePromotion,\n     StartDeploymentCommandHandler startDeployment,\n     CompletePromotionCommandHandler completePromotion,\n+    CancelPromotionCommandHandler cancelPromotion,\n     GetPromotionDetailsQueryHandler getPromotionDetails) : ControllerBase\n {\n     [HttpPost]\n@@ -79,6 +80,21 @@ await completePromotion.Handle(\n         return NoContent();\n     }\n \n+    [HttpPost(\"{id}/cancel\")]\n+    public async Task<IActionResult> CancelPromotion(\n+        string id,\n+        [FromHeader(Name = \"X-User-Id\")] string? actorHeader,\n+        CancellationToken cancellationToken)\n+    {\n+        var actorId = RequiredActor.Parse(actorHeader);\n+        var promotionId = ParsePromotionId(id);\n+\n+        await cancelPromotion.Handle(\n+            new CancelPromotionCommand(promotionId, actorId),\n+            cancellationToken);\n+        return NoContent();\n+    }\n+\n     [HttpGet(\"{id}\")]\n     public async Task<ActionResult<PromotionDetailsResponse>> GetPromotionDetails(\n         string id,\n@@ -0,0 +1,5 @@\n+namespace ReleaseManagement.Application;\n+\n+public sealed record CancelPromotionCommand(\n+    Guid PromotionId,\n+    Guid ActorId);\n@@ -0,0 +1,23 @@\n+using ReleaseManagement.Domain;\n+\n+namespace ReleaseManagement.Application;\n+\n+public sealed class CancelPromotionCommandHandler(\n+    IUserRepository users,\n+    IPromotionRepository promotions)\n+{\n+    public async Task Handle(\n+        CancelPromotionCommand command,\n+        CancellationToken cancellationToken)\n+    {\n+        var (actor, promotion) = await PromotionCommandContext.Load(\n+            users,\n+            promotions,\n+            command.ActorId,\n+            command.PromotionId,\n+            cancellationToken);\n+\n+        promotion.Cancel(actor, DateTimeOffset.UtcNow);\n+        await promotions.Update(promotion, cancellationToken);\n+    }\n+}\n@@ -0,0 +1,8 @@\n+namespace ReleaseManagement.Domain;\n+\n+public sealed record PromotionCancelled(\n+    DomainEventId Id,\n+    PromotionId PromotionId,\n+    DateTimeOffset OccurredAt,\n+    UserId ActorId)\n+    : PromotionDomainEvent(Id, PromotionId, OccurredAt, ActorId);\n@@ -154,6 +154,22 @@ public void Complete(Actor actor, DateTimeOffset completedAt)\n             actor.Id);\n     }\n \n+    public void Cancel(Actor actor, DateTimeOffset cancelledAt)\n+    {\n+        EnsureNotTerminal();\n+        if (Status is not PromotionStatus.Requested and not PromotionStatus.Approved)\n+        {\n+            throw new InvalidPromotionTransition();\n+        }\n+\n+        Status = PromotionStatus.Cancelled;\n+        UncommittedEvent = new PromotionCancelled(\n+            new DomainEventId(Guid.CreateVersion7()),\n+            Id,\n+            cancelledAt,\n+            actor.Id);\n+    }\n+\n     private void EnsureNotTerminal()\n     {\n         if (Status.IsTerminal())\n@@ -38,6 +38,10 @@ public static async Task Write(\n                 Type: \"promotion_completed\",\n                 Payload: \"{}\",\n                 Consumers: new[] { \"audit\", \"notification\" }),\n+            PromotionCancelled => (\n+                Type: \"promotion_cancelled\",\n+                Payload: \"{}\",\n+                Consumers: new[] { \"audit\" }),\n             _ => throw new UnreachableException()\n         };\n         await using (var command = new NpgsqlCommand(\n@@ -228,10 +228,87 @@ public void CompletedPromotionRejectsEveryLaterTransition()\n             () => promotion.StartDeployment(Actor, DateTimeOffset.UtcNow));\n         Assert.Throws<TerminalPromotionIsImmutable>(\n             () => promotion.Complete(Actor, DateTimeOffset.UtcNow));\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.Cancel(Actor, DateTimeOffset.UtcNow));\n         Assert.Equal(PromotionStatus.Completed, promotion.Status);\n         Assert.Same(uncommittedEvent, promotion.UncommittedEvent);\n     }\n \n+    [Theory]\n+    [InlineData(false)]\n+    [InlineData(true)]\n+    public void CancelsPromotionBeforeDeploymentAndRecordsItsEvent(bool approved)\n+    {\n+        var cancelledAt = DateTimeOffset.Parse(\"2026-07-23T12:10:00Z\");\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Actor,\n+            DateTimeOffset.Parse(\"2026-07-23T12:00:00Z\"));\n+        if (approved)\n+        {\n+            promotion.Approve(Approver, DateTimeOffset.Parse(\"2026-07-23T12:05:00Z\"));\n+        }\n+\n+        promotion.Cancel(Actor, cancelledAt);\n+\n+        Assert.Equal(PromotionStatus.Cancelled, promotion.Status);\n+        var domainEvent = Assert.IsType<PromotionCancelled>(promotion.UncommittedEvent);\n+        Assert.Equal(promotion.Id, domainEvent.PromotionId);\n+        Assert.Equal(Actor.Id, domainEvent.ActorId);\n+        Assert.Equal(cancelledAt, domainEvent.OccurredAt);\n+    }\n+\n+    [Fact]\n+    public void DeployingPromotionCannotBeCancelled()\n+    {\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Actor,\n+            DateTimeOffset.UtcNow);\n+        promotion.Approve(Approver, DateTimeOffset.UtcNow);\n+        promotion.StartDeployment(Actor, DateTimeOffset.UtcNow);\n+        var uncommittedEvent = promotion.UncommittedEvent;\n+\n+        Assert.Throws<InvalidPromotionTransition>(\n+            () => promotion.Cancel(Actor, DateTimeOffset.UtcNow));\n+        Assert.Equal(PromotionStatus.Deploying, promotion.Status);\n+        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);\n+    }\n+\n+    [Fact]\n+    public void CancelledPromotionRejectsEveryLaterTransition()\n+    {\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Actor,\n+            DateTimeOffset.UtcNow);\n+        promotion.Cancel(Actor, DateTimeOffset.UtcNow);\n+        var uncommittedEvent = promotion.UncommittedEvent;\n+\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.Approve(Approver, DateTimeOffset.UtcNow));\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.StartDeployment(Actor, DateTimeOffset.UtcNow));\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.Complete(Actor, DateTimeOffset.UtcNow));\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.Cancel(Actor, DateTimeOffset.UtcNow));\n+        Assert.Equal(PromotionStatus.Cancelled, promotion.Status);\n+        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);\n+    }\n+\n     [Theory]\n     [InlineData(DeploymentEnvironment.Dev, DeploymentEnvironment.Staging)]\n     [InlineData(DeploymentEnvironment.Staging, DeploymentEnvironment.Production)]\n@@ -0,0 +1,138 @@\n+using System.Net;\n+using System.Net.Http.Json;\n+using System.Text.Json;\n+\n+namespace ReleasePilot.IntegrationTests;\n+\n+public sealed class CancelPromotionTests : PromotionIntegrationTest\n+{\n+    private const string ApplicationId = \"01900000-0000-7000-8000-000000000101\";\n+    private const string VersionId = \"01900000-0000-7000-8000-000000000201\";\n+\n+    [Theory]\n+    [InlineData(false, 1, 2)]\n+    [InlineData(true, 2, 3)]\n+    public async Task CancelsPromotionBeforeDeployment(\n+        bool approved,\n+        int eventIndex,\n+        int historyLength)\n+    {\n+        Client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000002\");\n+        var createResponse = await Client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new { applicationVersionId = VersionId, targetEnvironment = \"dev\" });\n+        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())\n+            .GetProperty(\"id\")\n+            .GetString();\n+        if (approved)\n+        {\n+            Client.DefaultRequestHeaders.Remove(\"X-User-Id\");\n+            Client.DefaultRequestHeaders.Add(\n+                \"X-User-Id\",\n+                \"01900000-0000-7000-8000-000000000001\");\n+            Assert.Equal(\n+                HttpStatusCode.NoContent,\n+                (await Client.PostAsync($\"/promotions/{id}/approve\", null)).StatusCode);\n+        }\n+\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{id}/cancel\", null)).StatusCode);\n+\n+        var details = await Client.GetFromJsonAsync<JsonElement>($\"/promotions/{id}\");\n+        Assert.Equal(\"cancelled\", details.GetProperty(\"status\").GetString());\n+        Assert.Equal(\n+            \"promotion_cancelled\",\n+            details.GetProperty(\"history\")[eventIndex].GetProperty(\"type\").GetString());\n+        Assert.Equal(historyLength, details.GetProperty(\"history\").GetArrayLength());\n+        var repeatedCancellation = await Client.PostAsync($\"/promotions/{id}/cancel\", null);\n+        Assert.Equal(HttpStatusCode.Conflict, repeatedCancellation.StatusCode);\n+        Assert.Equal(\n+            \"terminal_promotion_is_immutable\",\n+            (await repeatedCancellation.Content.ReadFromJsonAsync<JsonElement>())\n+                .GetProperty(\"code\")\n+                .GetString());\n+    }\n+\n+    [Fact]\n+    public async Task RejectsCancellationAfterDeploymentStartsAndAfterCompletion()\n+    {\n+        Client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000002\");\n+        var createResponse = await Client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new { applicationVersionId = VersionId, targetEnvironment = \"dev\" });\n+        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())\n+            .GetProperty(\"id\")\n+            .GetString();\n+        Client.DefaultRequestHeaders.Remove(\"X-User-Id\");\n+        Client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000001\");\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{id}/approve\", null)).StatusCode);\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{id}/start-deployment\", null)).StatusCode);\n+\n+        var deployingCancellation = await Client.PostAsync($\"/promotions/{id}/cancel\", null);\n+        Assert.Equal(HttpStatusCode.Conflict, deployingCancellation.StatusCode);\n+        Assert.Equal(\n+            \"invalid_promotion_transition\",\n+            (await deployingCancellation.Content.ReadFromJsonAsync<JsonElement>())\n+                .GetProperty(\"code\")\n+                .GetString());\n+\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{id}/complete\", null)).StatusCode);\n+        var completedCancellation = await Client.PostAsync($\"/promotions/{id}/cancel\", null);\n+        Assert.Equal(HttpStatusCode.Conflict, completedCancellation.StatusCode);\n+        Assert.Equal(\n+            \"terminal_promotion_is_immutable\",\n+            (await completedCancellation.Content.ReadFromJsonAsync<JsonElement>())\n+                .GetProperty(\"code\")\n+                .GetString());\n+    }\n+\n+    [Fact]\n+    public async Task RetriesCancelledTargetWithNewIdentityAndPreservesBothAttempts()\n+    {\n+        Client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000002\");\n+        var cancelledResponse = await Client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new { applicationVersionId = VersionId, targetEnvironment = \"dev\" });\n+        var cancelledId = (await cancelledResponse.Content.ReadFromJsonAsync<JsonElement>())\n+            .GetProperty(\"id\")\n+            .GetString();\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{cancelledId}/cancel\", null)).StatusCode);\n+\n+        var retryResponse = await Client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new { applicationVersionId = VersionId, targetEnvironment = \"dev\" });\n+        var retryId = (await retryResponse.Content.ReadFromJsonAsync<JsonElement>())\n+            .GetProperty(\"id\")\n+            .GetString();\n+\n+        Assert.NotEqual(cancelledId, retryId);\n+        var history = await Client.GetFromJsonAsync<JsonElement>(\n+            $\"/applications/{ApplicationId}/promotions\");\n+        Assert.Equal(2, history.GetProperty(\"totalCount\").GetInt64());\n+        Assert.Contains(\n+            history.GetProperty(\"items\").EnumerateArray(),\n+            item => item.GetProperty(\"id\").GetString() == cancelledId\n+                && item.GetProperty(\"status\").GetString() == \"cancelled\");\n+        Assert.Contains(\n+            history.GetProperty(\"items\").EnumerateArray(),\n+            item => item.GetProperty(\"id\").GetString() == retryId\n+                && item.GetProperty(\"status\").GetString() == \"requested\");\n+    }\n+}",
>     "comments": [
>       {
>         "url": "https://github.com/jordialbert/ReleasePilot/pull/9#discussion_r3642216047",
>         "body": "**<sub><sub>![P1 Badge](https://img.shields.io/badge/P1-orange?style=flat)</sub></sub>  Serialize cancel against deployment start**\n\nWhen an Approved promotion receives `/start-deployment` and `/cancel` concurrently, both handlers can load the Approved snapshot; `StartDeploymentCommandHandler` invokes `deployment.Start` before it persists Deploying, so this cancellation update can win and store `cancelled`. The start request then fails its snapshot update after the external deployment has already been triggered, leaving a promotion that the API/history report as cancelled while deployment is actually underway. Make cancel and start share an atomic or locked state transition before the deployment port is invoked.\n\nUseful? React with 👍 / 👎.",
>         "user": {
>           "login": "chatgpt-codex-connector[bot]",
>           "name": "chatgpt-codex-connector[bot]",
>           "email": null,
>           "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>           "id": 199175422
>         },
>         "id": 3642216047,
>         "pull_request_review_id": 4769213511,
>         "in_reply_to_id": null,
>         "created_at": "2026-07-24T00:35:52Z",
>         "updated_at": "2026-07-24T00:35:52Z",
>         "review": null,
>         "side": "RIGHT",
>         "line": 20,
>         "start_line": null,
>         "path": "src/ReleaseManagement/Promotion/Application/Cancel/CancelPromotionCommandHandler.cs",
>         "body_html": "<p dir=\"auto\"><strong><sub><sub><a target=\"_blank\" rel=\"noopener noreferrer nofollow\" href=\"https://camo.githubusercontent.com/c595229c0ecb6ee85b9c7804144d495f131a495ec87091fea2b262d954c9a92d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50312d6f72616e67653f7374796c653d666c6174\"><img src=\"https://camo.githubusercontent.com/c595229c0ecb6ee85b9c7804144d495f131a495ec87091fea2b262d954c9a92d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50312d6f72616e67653f7374796c653d666c6174\" alt=\"P1 Badge\" data-canonical-src=\"https://img.shields.io/badge/P1-orange?style=flat\" style=\"max-width: 100%;\"></a></sub></sub>  Serialize cancel against deployment start</strong></p>\n<p dir=\"auto\">When an Approved promotion receives <code class=\"notranslate\">/start-deployment</code> and <code class=\"notranslate\">/cancel</code> concurrently, both handlers can load the Approved snapshot; <code class=\"notranslate\">StartDeploymentCommandHandler</code> invokes <code class=\"notranslate\">deployment.Start</code> before it persists Deploying, so this cancellation update can win and store <code class=\"notranslate\">cancelled</code>. The start request then fails its snapshot update after the external deployment has already been triggered, leaving a promotion that the API/history report as cancelled while deployment is actually underway. Make cancel and start share an atomic or locked state transition before the deployment port is invoked.</p>\n<p dir=\"auto\">Useful? React with 👍 / 👎.</p>"
>       },
>       {
>         "url": "https://github.com/jordialbert/ReleasePilot/pull/9#discussion_r3642216051",
>         "body": "**<sub><sub>![P2 Badge](https://img.shields.io/badge/P2-yellow?style=flat)</sub></sub>  Deliver cancellation events to notifications**\n\nFor a successful cancel, this only creates an `audit` delivery. Completed promotions are queued for `notification`, and cancellation is also a terminal outcome; without a `notification` delivery row for `promotion_cancelled`, the worker has nothing to claim and stakeholders never receive cancellation notifications. Add `notification` to the cancellation consumers.\n\nUseful? React with 👍 / 👎.",
>         "user": {
>           "login": "chatgpt-codex-connector[bot]",
>           "name": "chatgpt-codex-connector[bot]",
>           "email": null,
>           "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>           "id": 199175422
>         },
>         "id": 3642216051,
>         "pull_request_review_id": 4769213511,
>         "in_reply_to_id": null,
>         "created_at": "2026-07-24T00:35:52Z",
>         "updated_at": "2026-07-24T00:35:52Z",
>         "review": null,
>         "side": "RIGHT",
>         "line": 44,
>         "start_line": null,
>         "path": "src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionEventWriter.cs",
>         "body_html": "<p dir=\"auto\"><strong><sub><sub><a target=\"_blank\" rel=\"noopener noreferrer nofollow\" href=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\"><img src=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\" alt=\"P2 Badge\" data-canonical-src=\"https://img.shields.io/badge/P2-yellow?style=flat\" style=\"max-width: 100%;\"></a></sub></sub>  Deliver cancellation events to notifications</strong></p>\n<p dir=\"auto\">For a successful cancel, this only creates an <code class=\"notranslate\">audit</code> delivery. Completed promotions are queued for <code class=\"notranslate\">notification</code>, and cancellation is also a terminal outcome; without a <code class=\"notranslate\">notification</code> delivery row for <code class=\"notranslate\">promotion_cancelled</code>, the worker has nothing to claim and stakeholders never receive cancellation notifications. Add <code class=\"notranslate\">notification</code> to the cancellation consumers.</p>\n<p dir=\"auto\">Useful? React with 👍 / 👎.</p>"
>       },
>       {
>         "url": "https://github.com/jordialbert/ReleasePilot/pull/9#pullrequestreview-4769213511",
>         "body": "\n### 💡 Codex Review\n\nHere are some automated review suggestions for this pull request.\n\n**Reviewed commit:** `7e55685428`\n    \n\n<details> <summary>ℹ️ About Codex in GitHub</summary>\n<br/>\n\n[Your team has set up Codex to review pull requests in this repo](https://chatgpt.com/codex/cloud/settings/general). Reviews are triggered when you\n- Open a pull request for review\n- Mark a draft as ready\n- Comment \"@codex review\".\n\nIf Codex has suggestions, it will comment; otherwise it will react with 👍.\n\n\n\n\nCodex can also answer questions or update the PR. Try commenting \"@codex address that feedback\".\n            \n</details>",
>         "user": {
>           "login": "chatgpt-codex-connector[bot]",
>           "name": "chatgpt-codex-connector[bot]",
>           "email": null,
>           "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>           "id": 199175422
>         },
>         "id": 4769213511,
>         "pull_request_review_id": null,
>         "in_reply_to_id": null,
>         "created_at": "2026-07-24T00:35:52Z",
>         "updated_at": null,
>         "review": "COMMENTED",
>         "side": null,
>         "line": null,
>         "start_line": null,
>         "path": null,
>         "body_html": "<h3 dir=\"auto\">💡 Codex Review</h3>\n<p dir=\"auto\">Here are some automated review suggestions for this pull request.</p>\n<p dir=\"auto\"><strong>Reviewed commit:</strong> <code class=\"notranslate\">7e55685428</code></p>\n<details> <summary>ℹ️ About Codex in GitHub</summary>\n<br>\n<p dir=\"auto\"><a href=\"https://chatgpt.com/codex/cloud/settings/general\" rel=\"nofollow\">Your team has set up Codex to review pull requests in this repo</a>. Reviews are triggered when you</p>\n<ul dir=\"auto\">\n<li>Open a pull request for review</li>\n<li>Mark a draft as ready</li>\n<li>Comment \"<a class=\"user-mention notranslate\" data-hovercard-type=\"user\" data-hovercard-url=\"/users/codex/hovercard\" data-octo-click=\"hovercard-link-click\" data-octo-dimensions=\"link_type:self\" href=\"https://github.com/codex\">@codex</a> review\".</li>\n</ul>\n<p dir=\"auto\">If Codex has suggestions, it will comment; otherwise it will react with 👍.</p>\n<p dir=\"auto\">Codex can also answer questions or update the PR. Try commenting \"<a class=\"user-mention notranslate\" data-hovercard-type=\"user\" data-hovercard-url=\"/users/codex/hovercard\" data-octo-click=\"hovercard-link-click\" data-octo-dimensions=\"link_type:self\" href=\"https://github.com/codex\">@codex</a> address that feedback\".</p>\n</details>"
>       }
>     ],
>     "created_at": "2026-07-24T00:32:54Z",
>     "updated_at": "2026-07-24T00:35:52Z",
>     "closed_at": null,
>     "merged_at": null,
>     "commits": 4,
>     "changed_files": 9,
>     "additions": 288,
>     "deletions": 0,
>     "display_url": "https://github.com/jordialbert/ReleasePilot/pull/9",
>     "display_title": "feat: cancel promotions before deployment"
>   },
>   "url": "https://github.com/jordialbert/ReleasePilot/pull/9",
>   "title": "feat: cancel promotions before deployment",
>   "diff": "@@ -53,6 +53,7 @@\n builder.Services.AddScoped<ApprovePromotionCommandHandler>();\n builder.Services.AddScoped<StartDeploymentCommandHandler>();\n builder.Services.AddScoped<CompletePromotionCommandHandler>();\n+builder.Services.AddScoped<CancelPromotionCommandHandler>();\n builder.Services.AddScoped<GetPromotionDetailsQueryHandler>();\n builder.Services.AddScoped<ListPromotionsQueryHandler>();\n builder.Services.AddScoped<GetApplicationStatusQueryHandler>();\n@@ -11,6 +11,7 @@ public sealed class PromotionsController(\n     ApprovePromotionCommandHandler approvePromotion,\n     StartDeploymentCommandHandler startDeployment,\n     CompletePromotionCommandHandler completePromotion,\n+    CancelPromotionCommandHandler cancelPromotion,\n     GetPromotionDetailsQueryHandler getPromotionDetails) : ControllerBase\n {\n     [HttpPost]\n@@ -79,6 +80,21 @@ await completePromotion.Handle(\n         return NoContent();\n     }\n \n+    [HttpPost(\"{id}/cancel\")]\n+    public async Task<IActionResult> CancelPromotion(\n+        string id,\n+        [FromHeader(Name = \"X-User-Id\")] string? actorHeader,\n+        CancellationToken cancellationToken)\n+    {\n+        var actorId = RequiredActor.Parse(actorHeader);\n+        var promotionId = ParsePromotionId(id);\n+\n+        await cancelPromotion.Handle(\n+            new CancelPromotionCommand(promotionId, actorId),\n+            cancellationToken);\n+        return NoContent();\n+    }\n+\n     [HttpGet(\"{id}\")]\n     public async Task<ActionResult<PromotionDetailsResponse>> GetPromotionDetails(\n         string id,\n@@ -0,0 +1,5 @@\n+namespace ReleaseManagement.Application;\n+\n+public sealed record CancelPromotionCommand(\n+    Guid PromotionId,\n+    Guid ActorId);\n@@ -0,0 +1,23 @@\n+using ReleaseManagement.Domain;\n+\n+namespace ReleaseManagement.Application;\n+\n+public sealed class CancelPromotionCommandHandler(\n+    IUserRepository users,\n+    IPromotionRepository promotions)\n+{\n+    public async Task Handle(\n+        CancelPromotionCommand command,\n+        CancellationToken cancellationToken)\n+    {\n+        var (actor, promotion) = await PromotionCommandContext.Load(\n+            users,\n+            promotions,\n+            command.ActorId,\n+            command.PromotionId,\n+            cancellationToken);\n+\n+        promotion.Cancel(actor, DateTimeOffset.UtcNow);\n+        await promotions.Update(promotion, cancellationToken);\n+    }\n+}\n@@ -0,0 +1,8 @@\n+namespace ReleaseManagement.Domain;\n+\n+public sealed record PromotionCancelled(\n+    DomainEventId Id,\n+    PromotionId PromotionId,\n+    DateTimeOffset OccurredAt,\n+    UserId ActorId)\n+    : PromotionDomainEvent(Id, PromotionId, OccurredAt, ActorId);\n@@ -154,6 +154,22 @@ public void Complete(Actor actor, DateTimeOffset completedAt)\n             actor.Id);\n     }\n \n+    public void Cancel(Actor actor, DateTimeOffset cancelledAt)\n+    {\n+        EnsureNotTerminal();\n+        if (Status is not PromotionStatus.Requested and not PromotionStatus.Approved)\n+        {\n+            throw new InvalidPromotionTransition();\n+        }\n+\n+        Status = PromotionStatus.Cancelled;\n+        UncommittedEvent = new PromotionCancelled(\n+            new DomainEventId(Guid.CreateVersion7()),\n+            Id,\n+            cancelledAt,\n+            actor.Id);\n+    }\n+\n     private void EnsureNotTerminal()\n     {\n         if (Status.IsTerminal())\n@@ -38,6 +38,10 @@ public static async Task Write(\n                 Type: \"promotion_completed\",\n                 Payload: \"{}\",\n                 Consumers: new[] { \"audit\", \"notification\" }),\n+            PromotionCancelled => (\n+                Type: \"promotion_cancelled\",\n+                Payload: \"{}\",\n+                Consumers: new[] { \"audit\" }),\n             _ => throw new UnreachableException()\n         };\n         await using (var command = new NpgsqlCommand(\n@@ -228,10 +228,87 @@ public void CompletedPromotionRejectsEveryLaterTransition()\n             () => promotion.StartDeployment(Actor, DateTimeOffset.UtcNow));\n         Assert.Throws<TerminalPromotionIsImmutable>(\n             () => promotion.Complete(Actor, DateTimeOffset.UtcNow));\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.Cancel(Actor, DateTimeOffset.UtcNow));\n         Assert.Equal(PromotionStatus.Completed, promotion.Status);\n         Assert.Same(uncommittedEvent, promotion.UncommittedEvent);\n     }\n \n+    [Theory]\n+    [InlineData(false)]\n+    [InlineData(true)]\n+    public void CancelsPromotionBeforeDeploymentAndRecordsItsEvent(bool approved)\n+    {\n+        var cancelledAt = DateTimeOffset.Parse(\"2026-07-23T12:10:00Z\");\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Actor,\n+            DateTimeOffset.Parse(\"2026-07-23T12:00:00Z\"));\n+        if (approved)\n+        {\n+            promotion.Approve(Approver, DateTimeOffset.Parse(\"2026-07-23T12:05:00Z\"));\n+        }\n+\n+        promotion.Cancel(Actor, cancelledAt);\n+\n+        Assert.Equal(PromotionStatus.Cancelled, promotion.Status);\n+        var domainEvent = Assert.IsType<PromotionCancelled>(promotion.UncommittedEvent);\n+        Assert.Equal(promotion.Id, domainEvent.PromotionId);\n+        Assert.Equal(Actor.Id, domainEvent.ActorId);\n+        Assert.Equal(cancelledAt, domainEvent.OccurredAt);\n+    }\n+\n+    [Fact]\n+    public void DeployingPromotionCannotBeCancelled()\n+    {\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Actor,\n+            DateTimeOffset.UtcNow);\n+        promotion.Approve(Approver, DateTimeOffset.UtcNow);\n+        promotion.StartDeployment(Actor, DateTimeOffset.UtcNow);\n+        var uncommittedEvent = promotion.UncommittedEvent;\n+\n+        Assert.Throws<InvalidPromotionTransition>(\n+            () => promotion.Cancel(Actor, DateTimeOffset.UtcNow));\n+        Assert.Equal(PromotionStatus.Deploying, promotion.Status);\n+        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);\n+    }\n+\n+    [Fact]\n+    public void CancelledPromotionRejectsEveryLaterTransition()\n+    {\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Actor,\n+            DateTimeOffset.UtcNow);\n+        promotion.Cancel(Actor, DateTimeOffset.UtcNow);\n+        var uncommittedEvent = promotion.UncommittedEvent;\n+\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.Approve(Approver, DateTimeOffset.UtcNow));\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.StartDeployment(Actor, DateTimeOffset.UtcNow));\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.Complete(Actor, DateTimeOffset.UtcNow));\n+        Assert.Throws<TerminalPromotionIsImmutable>(\n+            () => promotion.Cancel(Actor, DateTimeOffset.UtcNow));\n+        Assert.Equal(PromotionStatus.Cancelled, promotion.Status);\n+        Assert.Same(uncommittedEvent, promotion.UncommittedEvent);\n+    }\n+\n     [Theory]\n     [InlineData(DeploymentEnvironment.Dev, DeploymentEnvironment.Staging)]\n     [InlineData(DeploymentEnvironment.Staging, DeploymentEnvironment.Production)]\n@@ -0,0 +1,138 @@\n+using System.Net;\n+using System.Net.Http.Json;\n+using System.Text.Json;\n+\n+namespace ReleasePilot.IntegrationTests;\n+\n+public sealed class CancelPromotionTests : PromotionIntegrationTest\n+{\n+    private const string ApplicationId = \"01900000-0000-7000-8000-000000000101\";\n+    private const string VersionId = \"01900000-0000-7000-8000-000000000201\";\n+\n+    [Theory]\n+    [InlineData(false, 1, 2)]\n+    [InlineData(true, 2, 3)]\n+    public async Task CancelsPromotionBeforeDeployment(\n+        bool approved,\n+        int eventIndex,\n+        int historyLength)\n+    {\n+        Client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000002\");\n+        var createResponse = await Client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new { applicationVersionId = VersionId, targetEnvironment = \"dev\" });\n+        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())\n+            .GetProperty(\"id\")\n+            .GetString();\n+        if (approved)\n+        {\n+            Client.DefaultRequestHeaders.Remove(\"X-User-Id\");\n+            Client.DefaultRequestHeaders.Add(\n+                \"X-User-Id\",\n+                \"01900000-0000-7000-8000-000000000001\");\n+            Assert.Equal(\n+                HttpStatusCode.NoContent,\n+                (await Client.PostAsync($\"/promotions/{id}/approve\", null)).StatusCode);\n+        }\n+\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{id}/cancel\", null)).StatusCode);\n+\n+        var details = await Client.GetFromJsonAsync<JsonElement>($\"/promotions/{id}\");\n+        Assert.Equal(\"cancelled\", details.GetProperty(\"status\").GetString());\n+        Assert.Equal(\n+            \"promotion_cancelled\",\n+            details.GetProperty(\"history\")[eventIndex].GetProperty(\"type\").GetString());\n+        Assert.Equal(historyLength, details.GetProperty(\"history\").GetArrayLength());\n+        var repeatedCancellation = await Client.PostAsync($\"/promotions/{id}/cancel\", null);\n+        Assert.Equal(HttpStatusCode.Conflict, repeatedCancellation.StatusCode);\n+        Assert.Equal(\n+            \"terminal_promotion_is_immutable\",\n+            (await repeatedCancellation.Content.ReadFromJsonAsync<JsonElement>())\n+                .GetProperty(\"code\")\n+                .GetString());\n+    }\n+\n+    [Fact]\n+    public async Task RejectsCancellationAfterDeploymentStartsAndAfterCompletion()\n+    {\n+        Client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000002\");\n+        var createResponse = await Client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new { applicationVersionId = VersionId, targetEnvironment = \"dev\" });\n+        var id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())\n+            .GetProperty(\"id\")\n+            .GetString();\n+        Client.DefaultRequestHeaders.Remove(\"X-User-Id\");\n+        Client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000001\");\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{id}/approve\", null)).StatusCode);\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{id}/start-deployment\", null)).StatusCode);\n+\n+        var deployingCancellation = await Client.PostAsync($\"/promotions/{id}/cancel\", null);\n+        Assert.Equal(HttpStatusCode.Conflict, deployingCancellation.StatusCode);\n+        Assert.Equal(\n+            \"invalid_promotion_transition\",\n+            (await deployingCancellation.Content.ReadFromJsonAsync<JsonElement>())\n+                .GetProperty(\"code\")\n+                .GetString());\n+\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{id}/complete\", null)).StatusCode);\n+        var completedCancellation = await Client.PostAsync($\"/promotions/{id}/cancel\", null);\n+        Assert.Equal(HttpStatusCode.Conflict, completedCancellation.StatusCode);\n+        Assert.Equal(\n+            \"terminal_promotion_is_immutable\",\n+            (await completedCancellation.Content.ReadFromJsonAsync<JsonElement>())\n+                .GetProperty(\"code\")\n+                .GetString());\n+    }\n+\n+    [Fact]\n+    public async Task RetriesCancelledTargetWithNewIdentityAndPreservesBothAttempts()\n+    {\n+        Client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000002\");\n+        var cancelledResponse = await Client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new { applicationVersionId = VersionId, targetEnvironment = \"dev\" });\n+        var cancelledId = (await cancelledResponse.Content.ReadFromJsonAsync<JsonElement>())\n+            .GetProperty(\"id\")\n+            .GetString();\n+        Assert.Equal(\n+            HttpStatusCode.NoContent,\n+            (await Client.PostAsync($\"/promotions/{cancelledId}/cancel\", null)).StatusCode);\n+\n+        var retryResponse = await Client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new { applicationVersionId = VersionId, targetEnvironment = \"dev\" });\n+        var retryId = (await retryResponse.Content.ReadFromJsonAsync<JsonElement>())\n+            .GetProperty(\"id\")\n+            .GetString();\n+\n+        Assert.NotEqual(cancelledId, retryId);\n+        var history = await Client.GetFromJsonAsync<JsonElement>(\n+            $\"/applications/{ApplicationId}/promotions\");\n+        Assert.Equal(2, history.GetProperty(\"totalCount\").GetInt64());\n+        Assert.Contains(\n+            history.GetProperty(\"items\").EnumerateArray(),\n+            item => item.GetProperty(\"id\").GetString() == cancelledId\n+                && item.GetProperty(\"status\").GetString() == \"cancelled\");\n+        Assert.Contains(\n+            history.GetProperty(\"items\").EnumerateArray(),\n+            item => item.GetProperty(\"id\").GetString() == retryId\n+                && item.GetProperty(\"status\").GetString() == \"requested\");\n+    }\n+}",
>   "comments": [
>     {
>       "url": "https://github.com/jordialbert/ReleasePilot/pull/9#discussion_r3642216047",
>       "body": "**<sub><sub>![P1 Badge](https://img.shields.io/badge/P1-orange?style=flat)</sub></sub>  Serialize cancel against deployment start**\n\nWhen an Approved promotion receives `/start-deployment` and `/cancel` concurrently, both handlers can load the Approved snapshot; `StartDeploymentCommandHandler` invokes `deployment.Start` before it persists Deploying, so this cancellation update can win and store `cancelled`. The start request then fails its snapshot update after the external deployment has already been triggered, leaving a promotion that the API/history report as cancelled while deployment is actually underway. Make cancel and start share an atomic or locked state transition before the deployment port is invoked.\n\nUseful? React with 👍 / 👎.",
>       "user": {
>         "login": "chatgpt-codex-connector[bot]",
>         "name": "chatgpt-codex-connector[bot]",
>         "email": null,
>         "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>         "id": 199175422
>       },
>       "id": 3642216047,
>       "pull_request_review_id": 4769213511,
>       "in_reply_to_id": null,
>       "created_at": "2026-07-24T00:35:52Z",
>       "updated_at": "2026-07-24T00:35:52Z",
>       "review": null,
>       "side": "RIGHT",
>       "line": 20,
>       "start_line": null,
>       "path": "src/ReleaseManagement/Promotion/Application/Cancel/CancelPromotionCommandHandler.cs",
>       "body_html": "<p dir=\"auto\"><strong><sub><sub><a target=\"_blank\" rel=\"noopener noreferrer nofollow\" href=\"https://camo.githubusercontent.com/c595229c0ecb6ee85b9c7804144d495f131a495ec87091fea2b262d954c9a92d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50312d6f72616e67653f7374796c653d666c6174\"><img src=\"https://camo.githubusercontent.com/c595229c0ecb6ee85b9c7804144d495f131a495ec87091fea2b262d954c9a92d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50312d6f72616e67653f7374796c653d666c6174\" alt=\"P1 Badge\" data-canonical-src=\"https://img.shields.io/badge/P1-orange?style=flat\" style=\"max-width: 100%;\"></a></sub></sub>  Serialize cancel against deployment start</strong></p>\n<p dir=\"auto\">When an Approved promotion receives <code class=\"notranslate\">/start-deployment</code> and <code class=\"notranslate\">/cancel</code> concurrently, both handlers can load the Approved snapshot; <code class=\"notranslate\">StartDeploymentCommandHandler</code> invokes <code class=\"notranslate\">deployment.Start</code> before it persists Deploying, so this cancellation update can win and store <code class=\"notranslate\">cancelled</code>. The start request then fails its snapshot update after the external deployment has already been triggered, leaving a promotion that the API/history report as cancelled while deployment is actually underway. Make cancel and start share an atomic or locked state transition before the deployment port is invoked.</p>\n<p dir=\"auto\">Useful? React with 👍 / 👎.</p>"
>     },
>     {
>       "url": "https://github.com/jordialbert/ReleasePilot/pull/9#discussion_r3642216051",
>       "body": "**<sub><sub>![P2 Badge](https://img.shields.io/badge/P2-yellow?style=flat)</sub></sub>  Deliver cancellation events to notifications**\n\nFor a successful cancel, this only creates an `audit` delivery. Completed promotions are queued for `notification`, and cancellation is also a terminal outcome; without a `notification` delivery row for `promotion_cancelled`, the worker has nothing to claim and stakeholders never receive cancellation notifications. Add `notification` to the cancellation consumers.\n\nUseful? React with 👍 / 👎.",
>       "user": {
>         "login": "chatgpt-codex-connector[bot]",
>         "name": "chatgpt-codex-connector[bot]",
>         "email": null,
>         "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>         "id": 199175422
>       },
>       "id": 3642216051,
>       "pull_request_review_id": 4769213511,
>       "in_reply_to_id": null,
>       "created_at": "2026-07-24T00:35:52Z",
>       "updated_at": "2026-07-24T00:35:52Z",
>       "review": null,
>       "side": "RIGHT",
>       "line": 44,
>       "start_line": null,
>       "path": "src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionEventWriter.cs",
>       "body_html": "<p dir=\"auto\"><strong><sub><sub><a target=\"_blank\" rel=\"noopener noreferrer nofollow\" href=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\"><img src=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\" alt=\"P2 Badge\" data-canonical-src=\"https://img.shields.io/badge/P2-yellow?style=flat\" style=\"max-width: 100%;\"></a></sub></sub>  Deliver cancellation events to notifications</strong></p>\n<p dir=\"auto\">For a successful cancel, this only creates an <code class=\"notranslate\">audit</code> delivery. Completed promotions are queued for <code class=\"notranslate\">notification</code>, and cancellation is also a terminal outcome; without a <code class=\"notranslate\">notification</code> delivery row for <code class=\"notranslate\">promotion_cancelled</code>, the worker has nothing to claim and stakeholders never receive cancellation notifications. Add <code class=\"notranslate\">notification</code> to the cancellation consumers.</p>\n<p dir=\"auto\">Useful? React with 👍 / 👎.</p>"
>     },
>     {
>       "url": "https://github.com/jordialbert/ReleasePilot/pull/9#pullrequestreview-4769213511",
>       "body": "\n### 💡 Codex Review\n\nHere are some automated review suggestions for this pull request.\n\n**Reviewed commit:** `7e55685428`\n    \n\n<details> <summary>ℹ️ About Codex in GitHub</summary>\n<br/>\n\n[Your team has set up Codex to review pull requests in this repo](https://chatgpt.com/codex/cloud/settings/general). Reviews are triggered when you\n- Open a pull request for review\n- Mark a draft as ready\n- Comment \"@codex review\".\n\nIf Codex has suggestions, it will comment; otherwise it will react with 👍.\n\n\n\n\nCodex can also answer questions or update the PR. Try commenting \"@codex address that feedback\".\n            \n</details>",
>       "user": {
>         "login": "chatgpt-codex-connector[bot]",
>         "name": "chatgpt-codex-connector[bot]",
>         "email": null,
>         "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>         "id": 199175422
>       },
>       "id": 4769213511,
>       "pull_request_review_id": null,
>       "in_reply_to_id": null,
>       "created_at": "2026-07-24T00:35:52Z",
>       "updated_at": null,
>       "review": "COMMENTED",
>       "side": null,
>       "line": null,
>       "start_line": null,
>       "path": null,
>       "body_html": "<h3 dir=\"auto\">💡 Codex Review</h3>\n<p dir=\"auto\">Here are some automated review suggestions for this pull request.</p>\n<p dir=\"auto\"><strong>Reviewed commit:</strong> <code class=\"notranslate\">7e55685428</code></p>\n<details> <summary>ℹ️ About Codex in GitHub</summary>\n<br>\n<p dir=\"auto\"><a href=\"https://chatgpt.com/codex/cloud/settings/general\" rel=\"nofollow\">Your team has set up Codex to review pull requests in this repo</a>. Reviews are triggered when you</p>\n<ul dir=\"auto\">\n<li>Open a pull request for review</li>\n<li>Mark a draft as ready</li>\n<li>Comment \"<a class=\"user-mention notranslate\" data-hovercard-type=\"user\" data-hovercard-url=\"/users/codex/hovercard\" data-octo-click=\"hovercard-link-click\" data-octo-dimensions=\"link_type:self\" href=\"https://github.com/codex\">@codex</a> review\".</li>\n</ul>\n<p dir=\"auto\">If Codex has suggestions, it will comment; otherwise it will react with 👍.</p>\n<p dir=\"auto\">Codex can also answer questions or update the PR. Try commenting \"<a class=\"user-mention notranslate\" data-hovercard-type=\"user\" data-hovercard-url=\"/users/codex/hovercard\" data-octo-click=\"hovercard-link-click\" data-octo-dimensions=\"link_type:self\" href=\"https://github.com/codex\">@codex</a> address that feedback\".</p>\n</details>"
>     }
>   ],
>   "display_url": "https://github.com/jordialbert/ReleasePilot/pull/9",
>   "display_title": "feat: cancel promotions before deployment"
> }
> ```
>
> Searched the web for `https://backend-challenge-2026.theagilemonkeys.com`
>
> Searched the web for `site:backend-challenge-2026.theagilemonkeys.com Release Pilot backend challenge 2026 cancellation deployment notifications`
>
> </details>
>
> There are two unresolved threads. The notification suggestion appears to belong to later issue 10 rather than issue 6, while the start-vs-cancel race may expose a real lifecycle inconsistency. I’m checking the published challenge and the plan’s deployment/idempotency requirements before deciding; the challenge URL isn’t indexable through normal web retrieval, so I’m reading the site response directly.
>
> The cross-check is decisive:
>
> 1. The start/cancel race is valid. It violates the challenge’s “cancel before deployment” rule and the plan’s guarantee that cancellation leaves deployment unchanged. I’ll serialize those two commands while preserving the required order: external deployment call first, persistence only after success.
> 2. The notification comment is also valid against the published challenge and `PLAN.md`/`SPEC.md`: `NotificationPort` covers terminal states, and the plan explicitly subscribes Cancelled events. The later issue 10 owns processing/idempotency, but this issue must create the delivery atomically so cancellations are not lost before that consumer exists.
>
> I’m using the concurrency-diagnosis skill for the first fix because it crosses the database/external-effect boundary; it will guide a focused repro and test rather than a broader redesign.
>
> For the race, the ranked hypotheses are:
>
> 1. Most likely: optimistic concurrency happens too late—after the external deployment effect—so cancel can commit first.
> 2. The deployment adapter’s idempotency prevents duplicates but cannot undo the first real deployment effect.
> 3. The active-target unique index does not serialize updates to the same Promotion row; it only protects competing Promotion creation.
> 4. Terminal guards correctly reject cancellation after Deploying, but both requests can observe Approved before either commits.
>
> I’m adding a deterministic integration repro that pauses the deployment port and verifies cancellation waits at PostgreSQL rather than committing. It should fail on the current branch, then pass once the shared lock is added.
>
> <details><summary>Edited 5 files</summary>
>
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+81 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Application/Cancel/CancelPromotionCommandHandler.cs` (+3 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Application/StartDeployment/StartDeploymentCommandHandler.cs` (+3 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/IPromotionRepository.cs` (+3 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs` (+24 -0)
>
> </details>
>
> The regression test failed on the original code and now passes with a PostgreSQL transaction-scoped advisory lock shared by start and cancel. This preserves the plan’s required external-call-before-commit order and Approved-on-port-failure behavior, while preventing cancellation from winning after deployment has begun. I’m now making the one-line terminal notification subscription change and asserting the exact consumer deliveries.
>
> <details><summary>Edited 2 files</summary>
>
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionEventWriter.cs` (+1 -1)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+14 -0)
>
> </details>
>
> Both focused suites pass after the fixes: 18 domain tests and 6 start/cancel integration tests. The diff remains scoped to the two review findings. I’m splitting it into two review-fix commits—serialization first, notification subscription second—then I’ll push the updated PR. Per the review workflow, I won’t post replies or resolve threads unless you explicitly ask me to.
>
> <details><summary>Called a tool</summary>
>
> MCP tool call
>
> codex_apps.github.list_pull_request_review_threads
>
> ```json
> {
>   "repository_full_name": "jordialbert/ReleasePilot",
>   "pr_number": 9
> }
> ```
>
> ```text
> InvalidActionArgumentsError: Invalid action arguments
> ```
>
> ```json
> {
>   "error": "InvalidActionArgumentsError: Invalid action arguments",
>   "error_code": "INVALID_ARGUMENT",
>   "error_data": {
>     "type": "invalid_action_arguments",
>     "code": "invalid_action_arguments",
>     "reason": "missing_required_argument",
>     "failure_stage": "argument_binding"
>   }
> }
> ```
>
> </details>
</details>

Fetched and analyzed both PR comments against the [published challenge](https://backend-challenge-2026.theagilemonkeys.com), `PLAN.md`, `SPEC.md`, and issue boundaries. Both made sense and were addressed:

- P1: Added a shared PostgreSQL transaction-scoped lock for start/cancel transitions. The required deployment-before-persistence ordering remains intact.
- P2: Cancellation events now enqueue both `audit` and `notification` deliveries.

Added two atomic commits and pushed them:

- `7e81f0e fix: serialize cancellation with deployment start`
- `99ded3f fix: enqueue cancellation notifications`

Validation:

- Domain: 18 passed
- Start/cancel integration: 6 passed
- [PR #9](https://github.com/jordialbert/ReleasePilot/pull/9) remains ready, clean, and mergeable.

The review threads remain unresolved because you didn’t explicitly authorize replies or thread resolution.

> is the transitionlock needed? isn't there another way of solving this?

The named `AcquireTransitionLock` is not inherently required, but some serialization mechanism is.

Optimistic concurrency alone is insufficient because deployment is triggered before persistence. If cancellation commits first, deployment has already happened even though the snapshot becomes Cancelled. Idempotency prevents duplicate deployments; it cannot undo that deployment.

A cleaner solution is a single transaction boundary:

1. Begin a database transaction.
2. Load the Promotion with `SELECT … FOR UPDATE`.
3. Apply the transition.
4. For start, invoke `DeploymentPort`.
5. Persist snapshot and event.
6. Commit.

A concurrent cancellation blocks on the row lock, then reloads Deploying and fails. Deployment failure rolls back, leaving Approved as required.

That would eliminate:

- The advisory lock.
- `AcquireTransitionLock` from the domain repository.
- The extra database connection held during the command.

The current transition lock was the smallest safe fix compatible with repositories that independently open connections. Architecturally, I’d prefer refactoring start/cancel around a shared transaction/unit-of-work and a PostgreSQL row lock. The other major alternative is an outbox-driven asynchronous deployment, but that changes the challenge’s command semantics and the plan’s required call-before-commit ordering.

> do that refactor

<details><summary>2 previous messages</summary>

> I’ll replace the advisory-lock API with a single PostgreSQL transaction that owns the row lock, load, external deployment call, snapshot update, and event write. I’m using the codebase-design skill because this changes the repository transaction boundary; the goal is to keep that mechanism out of the domain interface while preserving the challenge’s call-before-commit behavior.
>
> The design skill points to a deeper seam with one caller-facing operation that hides connection, transaction, row locking, and commit ordering. I’m reading its dependency guidance before choosing whether that seam belongs on the repository or in an application transaction abstraction.
</details>

> [$prototype](~/.agents/skills/prototype/SKILL.md) prototype the refactor change and how it would look like

<details><summary>5 previous messages</summary>

> I’m switching from implementation to a throwaway logic prototype. I’ll model the proposed single-transaction/row-lock interface as a tiny runnable terminal app under `.scratch`, so we can inspect the caller shape and the start-vs-cancel outcomes without touching the PR’s production code.
>
> The prototype question is: “Can a deep repository `Transition` operation hide the connection, transaction, row lock, reload, atomic event write, and commit—so handlers never see `AcquireTransitionLock` or a unit-of-work lifecycle—while preserving the two required race outcomes?”
>
> I’ll model both outcomes interactively: deployment succeeds while cancel waits, and deployment fails while cancel waits. The prototype will be isolated under `.scratch`, in memory only, and runnable with one `dotnet run` command.
>
> <details><summary>Created 5 files, edited a file</summary>
>
> - Wrote `./.scratch/release-pilot/prototypes/transaction-boundary/Program.cs` (+43 -0)
> - Wrote `./.scratch/release-pilot/prototypes/transaction-boundary/README.md` (+60 -0)
> - Wrote `./.scratch/release-pilot/prototypes/transaction-boundary/TransactionBoundaryPrototype.csproj` (+10 -0)
> - Wrote `./.scratch/release-pilot/prototypes/transaction-boundary/TransactionState.cs` (+10 -0)
> - Wrote `./.scratch/release-pilot/prototypes/transaction-boundary/TransitionModel.cs` (+127 -0)
> - Wrote `./.scratch/release-pilot/prototypes/transaction-boundary/TransitionModel.cs` (+7 -1)
>
> </details>
>
> The prototype is built around a deep `Transition` repository interface rather than exposing a unit-of-work object. That keeps handlers explicit about business orchestration and the deployment port, while the PostgreSQL adapter owns every transactional detail. I’m running both race sequences now to verify the model communicates the intended outcomes.
>
> The prototype confirms both intended outcomes with the proposed deep repository seam:
>
> - If deployment succeeds, Start commits Deploying; waiting Cancel reloads and conflicts.
> - If deployment fails, Start rolls back to Approved; waiting Cancel reloads and commits Cancelled.
>
> No production or PR code was changed. I’m doing a final scope check and will hand you the one-command runner plus the proposed interface location.
</details>

Created the throwaway transaction-boundary prototype:

[Prototype README](./.scratch/release-pilot/prototypes/transaction-boundary/README.md)

Run it with:

```sh
dotnet run --project .scratch/release-pilot/prototypes/transaction-boundary/TransactionBoundaryPrototype.csproj
```

Try:

- `s`, `c`, `d`: deployment succeeds; cancellation waits, reloads Deploying, and conflicts.
- `r`, `s`, `c`, `f`: deployment fails; transaction rolls back, then cancellation commits.

The proposed production interface is:

```csharp
Task<bool> Transition(
    PromotionId id,
    Func<Promotion, CancellationToken, Task> change,
    CancellationToken cancellationToken);
```

The PostgreSQL adapter hides `BEGIN`, `SELECT … FOR UPDATE`, reload, snapshot/event persistence, and commit. Handlers retain the visible business orchestration without exposing locks or unit-of-work lifecycle.

The prototype is uncommitted and does not alter the PR’s production code.

> try the solution in the codebase itself,then remove the prototype. do not commit or push any changes yet

<details><summary>5 previous messages</summary>

> I’ll replace the advisory-lock method with the prototyped deep `Transition` repository operation, move actor loading outside the transaction callback, and make PostgreSQL own `BEGIN → SELECT FOR UPDATE → transition → snapshot/event write → COMMIT`. I’ll update the concurrency test to observe the row-lock wait, run the focused start/cancel suites, then delete the prototype with no commit or push.
>
> <details><summary>Edited 5 files</summary>
>
> - Wrote `./src/ReleaseManagement/Promotion/Application/Cancel/CancelPromotionCommandHandler.cs` (+12 -10)
> - Wrote `./src/ReleaseManagement/Promotion/Application/StartDeployment/StartDeploymentCommandHandler.cs` (+19 -17)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/IPromotionRepository.cs` (+2 -1)
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs` (+64 -14)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+1 -1)
>
> </details>
>
> The production code now matches the prototype: handlers call one `Transition` operation, while PostgreSQL owns the row lock and the complete transaction. The domain interface no longer exposes `IAsyncDisposable` or lock lifecycle. I’m running the concurrency regression first because it directly distinguishes the old advisory-lock implementation from the row-lock transaction.
>
> The row-lock implementation passes the full focused verification, including the existing “deployment failure leaves Approved” scenario. The deep repository interface also removed the domain-visible lock lifecycle as intended. I’m deleting the prototype now and then auditing the final uncommitted diff for only the production refactor and its regression-test adjustment.
>
> <details><summary>Deleted 5 files</summary>
>
> - Deleted `./.scratch/release-pilot/prototypes/transaction-boundary/Program.cs` (+0 -43)
> - Deleted `./.scratch/release-pilot/prototypes/transaction-boundary/README.md` (+0 -60)
> - Deleted `./.scratch/release-pilot/prototypes/transaction-boundary/TransactionBoundaryPrototype.csproj` (+0 -10)
> - Deleted `./.scratch/release-pilot/prototypes/transaction-boundary/TransactionState.cs` (+0 -10)
> - Deleted `./.scratch/release-pilot/prototypes/transaction-boundary/TransitionModel.cs` (+0 -133)
>
> </details>
</details>

Refactor implemented locally, with no commit or push.

Changes:

- Replaced `AcquireTransitionLock` with a deep `Transition` repository operation.
- PostgreSQL now owns one transaction containing:
  - `SELECT … FOR UPDATE`
  - aggregate transition
  - DeploymentPort invocation
  - snapshot/event persistence
  - commit or automatic rollback
- Start and cancel handlers no longer manage lock lifecycle.
- Concurrency test now verifies PostgreSQL row-lock contention.
- Prototype and generated artifacts were removed completely.

Verification passed:

- Domain tests: 18
- Start/cancel integration tests: 6
- Concurrency regression: passed

The working tree contains only the five uncommitted refactor files.
