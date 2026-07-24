# Implement ticket 4 on main

> Move to main branch, pull latest changes and implement ticket 4

<details><summary>9 previous messages</summary>

> I’ll use the GitHub workflow to inspect ticket 4, then safely switch to `main`, pull, implement the smallest focused change, and run the relevant tests. First I’m checking the worktree so no local changes get overwritten.
>
> `main` was clean and has now fast-forwarded to the latest remote commit. I’m opening ticket 4 and mapping its acceptance criteria to the current code before editing.
>
> Ticket 4 is the local spec “Start an idempotent Deployment.” It requires `POST /promotions/{id}/start`, a pre-commit idempotent deployment call, atomic `DeploymentStarted`, safe `503 deployment_unavailable`, and focused domain/API tests. I’ve resolved the exact scope and am creating its specified feature branch before making the minimal implementation.
>
> <details><summary>Created 6 files, edited 7 files</summary>
>
> - Wrote `./apps/ReleasePilot/Api/Program.cs` (+4 -0)
> - Wrote `./apps/ReleasePilot/Api/Promotion/PromotionsController.cs` (+35 -0)
> - Wrote `./apps/ReleasePilot/Api/Shared/ProblemDetailsExceptionHandler.cs` (+5 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Application/Error/DeploymentUnavailable.cs` (+7 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Application/StartDeployment/StartDeploymentCommand.cs` (+5 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Application/StartDeployment/StartDeploymentCommandHandler.cs` (+41 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/Event/DeploymentStarted.cs` (+7 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/IDeploymentPort.cs` (+6 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/Promotion.cs` (+16 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/InMemoryDeploymentPort.cs` (+23 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs` (+55 -14)
> - Wrote `./tests/ReleaseManagement.Domain.Tests/Promotion/PromotionTests.cs` (+42 -0)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/RequestPromotionTests.cs` (+70 -0)
>
> </details>
>
> The vertical slice is in place: domain transition/event, port and idempotent in-memory adapter, pre-commit invocation, persistence, API route, and safe error mapping. I’m compiling and running the focused domain tests first; then I’ll run the single integration scenario against PostgreSQL.
>
> The domain suite passes. The first integration run exposed a real sequencing issue: the start request is returning a lifecycle conflict before reaching the simulated outage. I’m narrowing that to approval persistence versus service-instance wiring, then I’ll correct only the failing seam.
>
> <details><summary>Edited 2 files</summary>
>
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/RequestPromotionTests.cs` (+4 -1)
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs` (+17 -9)
>
> </details>
>
> The persistence regression was isolated to the new multi-statement delivery insert; splitting the delivery SQL by transition restored approval and kept start atomic. Both the existing approval regression test and the new ticket-4 scenario now pass. I’m doing the final focused class run and diff hygiene check.
>
> <details><summary>Edited 3 files</summary>
>
> - Wrote `./src/ReleaseManagement/Promotion/Application/StartDeployment/StartDeploymentCommandHandler.cs` (+1 -1)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/IPromotionRepository.cs` (+1 -0)
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs` (+69 -62)
>
> </details>
</details>

Implemented ticket 4 on `feat/start-deployment` after updating `main`.

Key changes:

- Added `POST /promotions/{id}/start`.
- Added the `DeploymentStarted` domain transition and event.
- Added an idempotent in-memory deployment adapter keyed by Promotion ID.
- Ensured deployment runs before transactional persistence.
- Returns safe `503 deployment_unavailable` responses.
- Preserves Approved state and event history on deployment failure.
- Added focused domain and integration coverage.

Verification passed:

- Domain tests: 9/9
- Promotion integration tests: 3/3
- `git diff --check`

Changes remain uncommitted on `feat/start-deployment`.

> Could we have specific repositories for the domain_events and event_deliveries tables? Would you recommend this change? What pros and cons does this have

<details><summary>1 previous message</summary>

> I’m using the codebase-design lens because this is really about module boundaries and whether splitting persistence makes the transaction easier or harder to reason about. I’ll evaluate it against the current atomic snapshot/event/delivery requirement.
</details>

Yes, but I would not introduce a repository per table yet.

Repositories should represent meaningful domain collections or persistence behaviours—not mirror database tables. Splitting the current implementation now would risk moving transaction orchestration into command handlers and weakening the guarantee that the Promotion snapshot, event, and delivery are committed atomically.

| Table | Recommended abstraction | Why |
|---|---|---|
| `domain_events` | Eventually `IDomainEventStore` | Append immutable events and read ordered history |
| `event_deliveries` | Eventually `IEventDeliveryQueue` | Claim, lease, acknowledge, retry, and fail deliveries |
| `promotions` | `IPromotionRepository` | Load and persist the Promotion aggregate |

The delivery abstraction should probably not be a Domain repository. Delivery records are infrastructure-level queue bookkeeping, so an interface such as `IEventDeliveryQueue` better expresses behaviours like `Claim`, `Complete`, and `Fail` than CRUD methods.

Pros:

- Keeps specialized SQL—history ordering, `SKIP LOCKED`, leases, retries—local.
- Prevents `PostgreSqlPromotionRepository` from growing into a database-wide module.
- Gives workers a focused persistence interface.
- Makes event and delivery behaviour independently testable.
- Produces better names than generic table-oriented CRUD.

Cons:

- All repositories must share the same connection and transaction.
- Command handlers must not learn the required persistence ordering.
- Separate repositories that each open their own connection could leave the snapshot committed without its event or deliveries.
- More interfaces and mocks can create shallow modules with little real leverage.
- Repository-per-table design leaks the relational schema into the application model.

My recommendation:

- Keep the current combined transactional write for ticket 4.
- When implementing audit processing and delivery claiming, introduce `IDomainEventStore` and `IEventDeliveryQueue`.
- Back them with a shared scoped transaction/unit of work.
- Keep one application-facing operation responsible for staging the Promotion snapshot, event, and deliveries before a single commit.

So the desirable shape is:

```text
Command handler
  → Promotion repository + event publisher
  → shared transaction
      ├── promotions
      ├── domain_events
      └── event_deliveries
  → one commit
```

I would avoid three independently committed repositories. The separation is valuable once event history and delivery processing develop substantial behaviour, but atomicity should remain hidden behind one deep transactional module.

> What changes would require to achieve the desirable shape?

<details><summary>1 previous message</summary>

> I’m applying the codebase-design lens again to map this onto the current implementation without turning it into repository-per-table plumbing. I’ll outline the smallest migration that preserves one atomic commit.
</details>

This would be a medium-sized architectural refactor—not simply adding two repositories. The key requirement is making all write adapters share one PostgreSQL connection and transaction.

## Recommended target

```text
Command handler
  ├── mutate Promotion
  ├── call external port when required
  ├── PromotionRepository.Save(snapshot)
  ├── DomainEventBus.Publish(events)
  └── UnitOfWork.Commit()

Shared PostgreSQL transaction
  ├── promotions
  ├── domain_events
  └── event_deliveries
```

### 1. Give Domain Events a common interface

Currently, `Promotion` exposes separate properties such as `RequestedEvent`, `ApprovedEvent`, and `StartedEvent`.

Introduce something like:

```csharp
public interface IDomainEvent
{
    DomainEventId Id { get; }
    PromotionId PromotionId { get; }
    DateTimeOffset OccurredAt { get; }
    UserId ActorId { get; }
}
```

Each event implements it. `Promotion` maintains an internal event collection and exposes:

```csharp
IReadOnlyCollection<IDomainEvent> PullDomainEvents();
```

That removes the need for persistence to inspect transition-specific properties.

### 2. Make the Promotion repository snapshot-only

The methods in [PostgreSqlPromotionRepository.cs](./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs:114) currently:

- Update the Promotion snapshot.
- Insert its Domain Event.
- Create delivery records.
- Commit the transaction.

Those responsibilities would be separated. `IPromotionRepository` would stage only the snapshot:

```csharp
Task Add(Promotion promotion, CancellationToken cancellationToken);
Task Update(Promotion promotion, CancellationToken cancellationToken);
```

The transition-specific `StartDeployment` persistence method in [IPromotionRepository.cs](./src/ReleaseManagement/Promotion/Domain/IPromotionRepository.cs:3) could eventually disappear in favour of a consistent snapshot update operation.

With raw Npgsql, making `Update` generic may require an internal persistence concurrency value such as PostgreSQL `xmin`. That value should remain outside the Domain model.

### 3. Add a Domain Event publishing seam

Add:

```csharp
public interface IDomainEventBus
{
    Task Publish(
        IReadOnlyCollection<IDomainEvent> events,
        CancellationToken cancellationToken);
}
```

Its PostgreSQL adapter would:

- Insert each event into `domain_events`.
- Serialize its event-specific payload.
- Create the appropriate `event_deliveries`.

Consumer selection belongs inside this adapter:

- Every event → `audit`
- `PromotionApproved` → `release_notes`
- Terminal events → `notification`

Command handlers should not know which consumers subscribe to which events.

An internal `PostgreSqlDomainEventStore` and delivery writer could sit behind this adapter, but they do not need public interfaces yet.

### 4. Introduce a shared PostgreSQL transaction

This is the most important change.

Create a scoped PostgreSQL session that lazily owns:

- One `NpgsqlConnection`
- One `NpgsqlTransaction`
- Commit and rollback lifecycle

Then expose the small application interface:

```csharp
public interface IUnitOfWork
{
    Task Commit(CancellationToken cancellationToken);
}
```

`PostgreSqlPromotionRepository` and `PostgreSqlDomainEventBus` both receive the same scoped session. Neither commits independently.

Disposal rolls back an uncommitted transaction automatically.

### 5. Change command orchestration

For approval, [ApprovePromotionCommandHandler.cs](./src/ReleaseManagement/Promotion/Application/Approve/ApprovePromotionCommandHandler.cs:5) would become conceptually:

```csharp
promotion.Approve(actor, now);

await promotions.Update(promotion, cancellationToken);
await events.Publish(promotion.PullDomainEvents(), cancellationToken);
await unitOfWork.Commit(cancellationToken);
```

For starting Deployment, [StartDeploymentCommandHandler.cs](./src/ReleaseManagement/Promotion/Application/StartDeployment/StartDeploymentCommandHandler.cs:5) keeps the external call before database staging:

```csharp
promotion.StartDeployment(actor, now);

await deployment.Start(promotion.Id, cancellationToken);

await promotions.Update(promotion, cancellationToken);
await events.Publish(promotion.PullDomainEvents(), cancellationToken);
await unitOfWork.Commit(cancellationToken);
```

Therefore an external failure still performs no database writes.

### 6. Change dependency-injection lifetimes

The repository is currently registered as a singleton in [Program.cs](./apps/ReleasePilot/Api/Program.cs:35).

Transactional adapters would become scoped:

```csharp
AddScoped<PostgreSqlSession>();
AddScoped<IUnitOfWork, PostgreSqlUnitOfWork>();
AddScoped<IPromotionRepository, PostgreSqlPromotionRepository>();
AddScoped<IDomainEventBus, PostgreSqlDomainEventBus>();
```

A singleton `NpgsqlDataSource` can remain the connection factory. Query readers that do not participate in command transactions can continue opening independent read connections.

### 7. Add worker-specific delivery persistence later

When implementing workers, introduce an `IEventDeliveryQueue` with behavioural operations rather than CRUD:

```csharp
ClaimPending(...)
Complete(...)
Fail(...)
```

This interface would own lease tokens, retries, acknowledgement, and `SKIP LOCKED`. Command handlers would never depend on it; event publishing only stages initial delivery records.

### 8. Adjust tests

Focused integration tests should prove:

- Snapshot, event, and deliveries commit together.
- A forced event insertion failure rolls back the snapshot.
- A failed commit exposes a controlled error.
- Deployment failure produces no staged database work.
- `PullDomainEvents()` returns each event exactly once.
- Each event type creates only its subscribed deliveries.

## Recommendation on timing

I would make this a dedicated refactoring commit after ticket 4 and before ticket 5. Ticket 5 adds more lifecycle transitions, so delaying further will duplicate the current snapshot/event/delivery transaction code for every new command.

I would not introduce public `IDomainEventRepository` and `IEventDeliveryRepository` interfaces merely to mirror tables. The deeper application-facing shape is:

- `IPromotionRepository`
- `IDomainEventBus`
- `IUnitOfWork`
- Later, worker-only `IEventDeliveryQueue`

That gives callers a small interface while keeping PostgreSQL transaction and subscription complexity local to the infrastructure implementation.

> Send this discussion to fable-5 using cursor’s cli with the right amount of context about the discussion and run it in the current directory so it has the project context too, and ask its opinion, alternative solutions, and pros and cons of each one. Then send here in this thread fable’s opinion and I will make a final decision

<details><summary>5 previous messages</summary>

> I’ll invoke Fable-5 through Cursor’s CLI from this repository, giving it the architectural context, current implementation shape, and the options we discussed. I’ll ask for an independent recommendation, alternatives, and explicit trade-offs, then bring its response back here unchanged in substance.
>
> Fable-5 is available as a high-reasoning model. I’m sending it a read-only architecture review from the repository root, including the current transaction implementation, the proposed shared-unit-of-work shape, project constraints, and the timing question before ticket 5.
>
> Fable is still inspecting the repository and composing the comparison; the read-only run is active. I’m waiting for the complete memo so I can relay its actual recommendation rather than summarize a partial response.
>
> The model has not emitted partial text, but the CLI process remains healthy. It is running in ask/read-only mode from `release-pilot`, so it has the requested project context and cannot alter the worktree.
>
> Fable is taking a few minutes on the repository-level review. I’m continuing to wait for the final response; no project files have been changed.
</details>

Fable-5 inspected the repository in read-only mode from the project root. Its recommendation is decisive: do not introduce the shared-session/unit-of-work/event-bus design.

## Fable’s recommendation

Keep `IPromotionRepository` as the single public transactional write interface. After committing ticket 4, but before ticket 5, extract the duplicated `domain_events` and `event_deliveries` SQL into an internal infrastructure writer.

Its core conclusion:

> The current repository’s biggest flaw is duplication, not structure. Its deep transactional shape makes the critical invariant difficult to violate.

### Why it rejects the proposed Unit of Work

Fable identified these risks:

- Atomicity becomes conventional rather than structural: every handler must remember to stage the snapshot, publish events, create deliveries, and commit.
- A request-scoped transaction might accidentally be opened before the external Deployment call, holding locks and a pooled connection during network I/O.
- “Staging” with raw Npgsql means either executing SQL immediately or implementing a homemade change tracker.
- `PullDomainEvents()` can lose events if they are drained before a failed commit.
- A generic snapshot `Save` could lose the current compare-and-set protection:

```sql
UPDATE promotions
SET status = 'deploying'
WHERE id = $1 AND status = 'approved'
```

- Scoped session lifetimes and manual worker scopes add significant DI complexity.
- No planned command currently needs multiple public persistence modules to participate in the same transaction.

## Alternatives considered

### A. Keep everything unchanged

Pros:

- Zero additional changes.
- Atomicity remains structural.
- Existing guarded transitions and error mapping remain intact.

Cons:

- The repository already contains repeated event and delivery SQL.
- Completion, cancellation, and rollback would add more duplication.
- Consumer routing could become inconsistent across copied blocks.

### B. Internal event writer, one public repository — recommended

Add an internal `DomainEventSqlWriter` under `Promotion/Infrastructure`. Existing repository methods would pass their active `NpgsqlConnection` and `NpgsqlTransaction` to it.

The writer would own:

- Event insertion.
- Payload serialization.
- Event-type-to-consumer subscription mapping.
- Initial delivery insertion.

Pros:

- Removes existing and future duplication.
- Preserves repository-owned transactions.
- Requires no handler, DI, or public interface changes.
- Keeps the external Deployment call structurally outside the transaction.
- Makes ticket 5–7 persistence methods substantially smaller.

Cons:

- Initially specific to Promotion events.
- Does not address unrelated concerns currently present in `IPromotionRepository`, such as actor and version lookup.
- The proposed internal writer must be treated as a meaningful infrastructure module, given the project instruction against adding arbitrary helpers.

### C. Separate public stores sharing a Unit of Work

Pros:

- Textbook separation.
- Individual persistence implementations can be tested independently.
- Suitable for systems that genuinely coordinate several independently useful write modules.

Cons:

- Large refactor and DI overhaul.
- Weakens the atomicity guarantee at the handler interface.
- Introduces capabilities no current ticket needs.
- Makes raw Npgsql transaction ownership and error translation harder.
- Considered speculative under this project’s constraints.

### D. Database trigger for delivery fan-out

A trigger could create delivery rows whenever a Domain Event is inserted.

Pros:

- Delivery creation becomes impossible for application code to forget.
- Strong database-level atomicity.

Cons:

- Hides routing policy in PostgreSQL.
- Makes behaviour harder to discover, test, and review.
- Does not fit the current application-managed persistence style.

Fable does not recommend it here.

## Fable’s proposed ownership

| Concern | Owner |
|---|---|
| Promotion snapshot, transaction and commit | `PostgreSqlPromotionRepository` |
| Event append and initial delivery creation | Internal `DomainEventSqlWriter` |
| Payload serialization | Same internal infrastructure area |
| Event type → consumer mapping | Colocated with the event writer |
| Worker claim, lease, completion and failure | Future public `IEventDeliveryQueue` |
| Worker transaction lifecycle | Each queue operation |
| `IDomainEventBus`, `IUnitOfWork`, shared scoped session | Not justified on the current roadmap |

The future `IEventDeliveryQueue` should expose behaviours such as `Claim`, `Complete`, and `Fail`, rather than CRUD operations. It should be introduced when the worker tickets give deliveries real behaviour.

## Smallest migration Fable recommends

1. Commit ticket 4 unchanged.
2. Create a separate behavior-preserving refactor.
3. Extract event serialization, insertion, subscription mapping, and delivery insertion into the internal writer.
4. Keep transactions and commits inside `Add`, `Update`, `StartDeployment`, and future transition methods.
5. Add integration assertions for the exact delivery rows created for each event.
6. Implement ticket 5 using the extracted writer.
7. Introduce `IEventDeliveryQueue` only with the first worker ticket.

Fable also noted one separate concern: `StartDeploymentCommandHandler` currently translates only `HttpRequestException`. A future real adapter may also experience timeout-related exceptions. Fable recommends having the adapter translate its own failures into the application-safe deployment error rather than broadening exception knowledge in the handler.

No files were changed by the Cursor review.

> for this project that mainly revolves around promotion, I think that Fable's suggestion is a great middle ground to make the promotionrepository not have all the db code and logic and become unmaintainable, while not adding extra pieces in case new aggregates would be added to the project and risking not having atomicity at the handler interface.
>
> Log a 4.5 ticket explaining what will be needed.
>
> Additionally, I have found other parts that exist in the promotionsrepository that should be moved. Application queries and Actor queries should have their respectives repositories under their own directories. Log this too to the 4.5 ticket

<details><summary>4 previous messages</summary>

> I’ll use the issue-writing workflow to capture this as ticket 4.5 in the project’s existing local-ticket format. I’ll include both the internal event/delivery writer refactor and moving actor/application-version queries into repositories owned by their modules, while keeping atomicity inside Promotion repository methods.
>
> The existing tickets were stored under `.scratch/release-pilot/issues` and later ignored individually. I’ll add `04.5` there without touching the current ticket-4 implementation, using the same concise format and making ticket 5 explicitly depend on this refactor.
>
> <details><summary>Created a file, edited a file</summary>
>
> - Wrote `./.scratch/release-pilot/issues/04.5-refactor-promotion-persistence.md` (+30 -0)
> - Wrote `./.scratch/release-pilot/issues/05-complete-promotion.md` (+1 -1)
>
> </details>
>
> Ticket 4.5 is now drafted as a behavior-preserving refactor: one public Promotion write seam and repository-owned transaction, one internal event/delivery writer, plus User-owned actor lookup and Application-Version-owned version lookup. I also updated ticket 5 to depend on 4.5 so the sequence is explicit.
</details>

Logged ticket [04.5 — Separate Promotion persistence responsibilities](./.scratch/release-pilot/issues/04.5-refactor-promotion-persistence.md).

It covers:

- Internal Domain Event/delivery writer.
- Repository-owned atomic transactions.
- Actor lookup moved to the User module.
- Application Version lookup moved to the Application Version module.
- Promotion-owned queries remaining in Promotion.
- Focused atomicity and delivery tests.
- No speculative Unit of Work or public event/delivery repositories.

Ticket [05](./.scratch/release-pilot/issues/05-complete-promotion.md) now depends on 4.5.

`git diff --check` passes. An unrelated untracked `release-pilot.sln` appeared in the worktree; I left it untouched.

> cool. Now commit the changes for ticket 4 and open a ready for review PR

<details><summary>5 previous messages</summary>

> I’ll use the GitHub publish workflow to commit only ticket 4’s implementation and tests, leaving ticket 4.5 and the unrelated `release-pilot.sln` out of the commit. Then I’ll push `feat/start-deployment` and open a non-draft PR ready for review.
>
> Scope is cleanly separable. I’m staging the 13 ticket-4 implementation/test files explicitly; ticket 4.5’s two documentation changes and `release-pilot.sln` will remain unstaged and outside the PR commit.
>
> Ticket 4 is committed as `0e0f661` with only its implementation and tests. The ticket 4.5 notes remain local and uncommitted. I’m pushing the feature branch now, then I’ll create the PR directly as ready for review.
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
>   "head": "feat/start-deployment",
>   "title": "feat: start Deployment for Approved Promotions",
>   "body": "## Summary\n\n- add `POST /promotions/{id}/start` for successful `204 No Content` transitions\n- invoke an idempotent in-memory Deployment adapter using the Promotion ID\n- persist the Deploying snapshot and exactly one `DeploymentStarted` event atomically\n- return safe `503 deployment_unavailable` Problem Details without persisting failed starts\n\n## Why\n\nTicket 4 requires Approved Promotions to start external Deployment without duplicate execution or recording a transition when the external system is unavailable.\n\n## Impact\n\nRelease managers can start Approved Promotions through the public API. Invalid lifecycle transitions remain controlled conflicts, repeated external requests are deduplicated, and Deployment failures leave the stored Promotion Approved with unchanged history.\n\n## Verification\n\n- `dotnet test tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj --no-restore`\n- `dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj --filter FullyQualifiedName~RequestPromotionTests --no-restore`\n- `git diff --check`",
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
>   "url": "https://github.com/jordialbert/ReleasePilot/pull/5",
>   "number": 5,
>   "state": "open",
>   "merged": false,
>   "mergeable": false,
>   "draft": false,
>   "body": "## Summary\n\n- add `POST /promotions/{id}/start` for successful `204 No Content` transitions\n- invoke an idempotent in-memory Deployment adapter using the Promotion ID\n- persist the Deploying snapshot and exactly one `DeploymentStarted` event atomically\n- return safe `503 deployment_unavailable` Problem Details without persisting failed starts\n\n## Why\n\nTicket 4 requires Approved Promotions to start external Deployment without duplicate execution or recording a transition when the external system is unavailable.\n\n## Impact\n\nRelease managers can start Approved Promotions through the public API. Invalid lifecycle transitions remain controlled conflicts, repeated external requests are deduplicated, and Deployment failures leave the stored Promotion Approved with unchanged history.\n\n## Verification\n\n- `dotnet test tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj --no-restore`\n- `dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj --filter FullyQualifiedName~RequestPromotionTests --no-restore`\n- `git diff --check`",
>   "title": "feat: start Deployment for Approved Promotions",
>   "base": "main",
>   "base_sha": "bf9b5ca509285bc1e8c40ab7702f5cc5da668243",
>   "head": "feat/start-deployment",
>   "head_sha": "0e0f6619f288ee6622d12ec0bbdb8f13aac734a5",
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
>   "created_at": "2026-07-23T20:57:18Z",
>   "updated_at": "2026-07-23T20:57:19Z",
>   "closed_at": null,
>   "merged_at": null,
>   "commits": 1,
>   "changed_files": 14,
>   "additions": 322,
>   "deletions": 1,
>   "display_url": "https://github.com/jordialbert/ReleasePilot/pull/5",
>   "display_title": "feat: start Deployment for Approved Promotions"
> }
> ```
>
> </details>
>
> PR #5 is open against `main` and marked ready for review (`draft: false`). I’m doing one final metadata check so I can hand back the exact review state and any immediate CI status.
</details>

Ticket 4 is committed and published.

- Commit: `0e0f661` — `feat: start Deployment for Approved Promotions`
- Branch: `feat/start-deployment`
- PR: [#5 — feat: start Deployment for Approved Promotions](https://github.com/jordialbert/ReleasePilot/pull/5)
- State: Open, ready for review, cleanly mergeable
- Target: `main`
- CI checks: none configured

Validation passed:

- Domain tests: 9/9
- Promotion integration tests: 3/3
- `git diff --check`

Ticket 4.5 documentation and `release-pilot.sln` remain local and were excluded from the commit and PR.

> address the comments in the PR

<details><summary>7 previous messages</summary>

> I’ll use the GitHub review-feedback workflow to inspect every PR #5 review thread, determine what is actionable, make the smallest fixes, run focused verification, and update the PR branch. I’ll keep the local ticket 4.5 notes and unrelated solution file out of the changes.
>
> <details><summary>Called a tool</summary>
>
> MCP tool call
>
> codex_apps.github.fetch_pr
>
> ```json
> {
>   "repo_full_name": "jordialbert/ReleasePilot",
>   "pr_number": 5
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
>     "url": "https://github.com/jordialbert/ReleasePilot/pull/5",
>     "number": 5,
>     "state": "open",
>     "merged": false,
>     "mergeable": true,
>     "draft": false,
>     "body": "## Summary\n\n- add `POST /promotions/{id}/start` for successful `204 No Content` transitions\n- invoke an idempotent in-memory Deployment adapter using the Promotion ID\n- persist the Deploying snapshot and exactly one `DeploymentStarted` event atomically\n- return safe `503 deployment_unavailable` Problem Details without persisting failed starts\n\n## Why\n\nTicket 4 requires Approved Promotions to start external Deployment without duplicate execution or recording a transition when the external system is unavailable.\n\n## Impact\n\nRelease managers can start Approved Promotions through the public API. Invalid lifecycle transitions remain controlled conflicts, repeated external requests are deduplicated, and Deployment failures leave the stored Promotion Approved with unchanged history.\n\n## Verification\n\n- `dotnet test tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj --no-restore`\n- `dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj --filter FullyQualifiedName~RequestPromotionTests --no-restore`\n- `git diff --check`",
>     "title": "feat: start Deployment for Approved Promotions",
>     "base": "main",
>     "base_sha": "bf9b5ca509285bc1e8c40ab7702f5cc5da668243",
>     "head": "feat/start-deployment",
>     "head_sha": "0e0f6619f288ee6622d12ec0bbdb8f13aac734a5",
>     "merge_commit_sha": "1134890dc9b5330905769a31191c20852aa902e7",
>     "user": {
>       "login": "jordialbert",
>       "name": "jordialbert",
>       "email": null,
>       "avatar_url": "https://avatars.githubusercontent.com/u/63541019?v=4",
>       "id": 63541019
>     },
>     "requested_reviewers": null,
>     "requested_team_reviewers": null,
>     "diff": "@@ -36,8 +36,12 @@\n     services => services.GetRequiredService<PostgreSqlPromotionRepository>());\n builder.Services.AddSingleton<IPromotionDetailsReader>(\n     services => services.GetRequiredService<PostgreSqlPromotionRepository>());\n+builder.Services.AddSingleton<InMemoryDeploymentPort>();\n+builder.Services.AddSingleton<IDeploymentPort>(\n+    services => services.GetRequiredService<InMemoryDeploymentPort>());\n builder.Services.AddScoped<RequestPromotionCommandHandler>();\n builder.Services.AddScoped<ApprovePromotionCommandHandler>();\n+builder.Services.AddScoped<StartDeploymentCommandHandler>();\n builder.Services.AddScoped<GetPromotionDetailsQueryHandler>();\n builder.Services\n     .AddHealthChecks()\n@@ -8,6 +8,7 @@ namespace ReleasePilot.Api.Promotion;\n public sealed class PromotionsController(\n     RequestPromotionCommandHandler requestPromotion,\n     ApprovePromotionCommandHandler approvePromotion,\n+    StartDeploymentCommandHandler startDeployment,\n     GetPromotionDetailsQueryHandler getPromotionDetails) : ControllerBase\n {\n     [HttpPost]\n@@ -81,6 +82,40 @@ await approvePromotion.Handle(\n         return NoContent();\n     }\n \n+    [HttpPost(\"{id}/start\")]\n+    public async Task<IActionResult> StartDeployment(\n+        string id,\n+        [FromHeader(Name = \"X-User-Id\")] string? actorHeader,\n+        CancellationToken cancellationToken)\n+    {\n+        if (actorHeader is null)\n+        {\n+            var problem = new ProblemDetails\n+            {\n+                Status = StatusCodes.Status401Unauthorized,\n+                Title = \"The X-User-Id header is required.\"\n+            };\n+            problem.Extensions[\"code\"] = \"missing_actor\";\n+            problem.Extensions[\"traceId\"] = HttpContext.TraceIdentifier;\n+            return Unauthorized(problem);\n+        }\n+\n+        if (!Guid.TryParse(actorHeader, out var actorId))\n+        {\n+            throw new InvalidInput(\"X-User-Id\");\n+        }\n+\n+        if (!Guid.TryParse(id, out var promotionId))\n+        {\n+            throw new InvalidInput(\"id\");\n+        }\n+\n+        await startDeployment.Handle(\n+            new StartDeploymentCommand(promotionId, actorId),\n+            cancellationToken);\n+        return NoContent();\n+    }\n+\n     [HttpGet(\"{id}\")]\n     public async Task<ActionResult<PromotionDetailsResponse>> GetPromotionDetails(\n         string id,\n@@ -37,6 +37,11 @@ public async ValueTask<bool> TryHandleAsync(\n                 code = resourceNotFound.Code;\n                 title = resourceNotFound.Message;\n                 break;\n+            case DeploymentUnavailable deploymentUnavailable:\n+                status = StatusCodes.Status503ServiceUnavailable;\n+                code = deploymentUnavailable.Code;\n+                title = deploymentUnavailable.Message;\n+                break;\n             case OnlyApproverCanApprove onlyApproverCanApprove:\n                 status = StatusCodes.Status403Forbidden;\n                 code = onlyApproverCanApprove.Code;\n@@ -0,0 +1,7 @@\n+namespace ReleaseManagement.Application;\n+\n+public sealed class DeploymentUnavailable()\n+    : Exception(\"The Deployment system is unavailable.\")\n+{\n+    public string Code => \"deployment_unavailable\";\n+}\n@@ -0,0 +1,5 @@\n+namespace ReleaseManagement.Application;\n+\n+public sealed record StartDeploymentCommand(\n+    Guid PromotionId,\n+    Guid ActorId);\n@@ -0,0 +1,41 @@\n+using ReleaseManagement.Domain;\n+\n+namespace ReleaseManagement.Application;\n+\n+public sealed class StartDeploymentCommandHandler(\n+    IPromotionRepository promotions,\n+    IDeploymentPort deployment)\n+{\n+    public async Task Handle(\n+        StartDeploymentCommand command,\n+        CancellationToken cancellationToken)\n+    {\n+        var actor = await promotions.FindActor(\n+            new UserId(command.ActorId),\n+            cancellationToken);\n+        if (actor is null)\n+        {\n+            throw new UnknownActor();\n+        }\n+\n+        var promotion = await promotions.Find(\n+            new PromotionId(command.PromotionId),\n+            cancellationToken);\n+        if (promotion is null)\n+        {\n+            throw new ResourceNotFound(\"promotion\");\n+        }\n+\n+        promotion.StartDeployment(actor, DateTimeOffset.UtcNow);\n+        try\n+        {\n+            await deployment.Start(promotion.Id, cancellationToken);\n+        }\n+        catch (Exception)\n+        {\n+            throw new DeploymentUnavailable();\n+        }\n+\n+        await promotions.StartDeployment(promotion, cancellationToken);\n+    }\n+}\n@@ -0,0 +1,7 @@\n+namespace ReleaseManagement.Domain;\n+\n+public sealed record DeploymentStarted(\n+    DomainEventId Id,\n+    PromotionId PromotionId,\n+    DateTimeOffset OccurredAt,\n+    UserId ActorId);\n@@ -0,0 +1,6 @@\n+namespace ReleaseManagement.Domain;\n+\n+public interface IDeploymentPort\n+{\n+    Task Start(PromotionId promotionId, CancellationToken cancellationToken);\n+}\n@@ -16,4 +16,5 @@ Task<bool> HasActivePromotion(\n     Task<Promotion?> Find(PromotionId id, CancellationToken cancellationToken);\n     Task Add(Promotion promotion, CancellationToken cancellationToken);\n     Task Update(Promotion promotion, CancellationToken cancellationToken);\n+    Task StartDeployment(Promotion promotion, CancellationToken cancellationToken);\n }\n@@ -54,6 +54,7 @@ internal Promotion(\n     public DateTimeOffset RequestedAt { get; }\n     public PromotionRequested? RequestedEvent { get; }\n     public PromotionApproved? ApprovedEvent { get; private set; }\n+    public DeploymentStarted? StartedEvent { get; private set; }\n \n     public static Promotion Request(\n         PromotionId id,\n@@ -123,4 +124,19 @@ public void Approve(Actor actor, DateTimeOffset approvedAt)\n             approvedAt,\n             actor.Id);\n     }\n+\n+    public void StartDeployment(Actor actor, DateTimeOffset startedAt)\n+    {\n+        if (Status != PromotionStatus.Approved)\n+        {\n+            throw new InvalidPromotionTransition();\n+        }\n+\n+        Status = PromotionStatus.Deploying;\n+        StartedEvent = new DeploymentStarted(\n+            new DomainEventId(Guid.CreateVersion7()),\n+            Id,\n+            startedAt,\n+            actor.Id);\n+    }\n }\n@@ -0,0 +1,23 @@\n+using System.Collections.Concurrent;\n+using ReleaseManagement.Domain;\n+\n+namespace ReleaseManagement.Infrastructure;\n+\n+public sealed class InMemoryDeploymentPort : IDeploymentPort\n+{\n+    private readonly ConcurrentDictionary<PromotionId, byte> requests = new();\n+\n+    public bool Unavailable { get; set; }\n+    public ICollection<PromotionId> Requests => requests.Keys;\n+\n+    public Task Start(PromotionId promotionId, CancellationToken cancellationToken)\n+    {\n+        if (Unavailable)\n+        {\n+            throw new Exception(\"Simulated Deployment system failure.\");\n+        }\n+\n+        requests.TryAdd(promotionId, 0);\n+        return Task.CompletedTask;\n+    }\n+}\n@@ -259,7 +259,6 @@ UPDATE promotions\n                 throw new InvalidPromotionTransition();\n             }\n         }\n-\n         var domainEvent = promotion.ApprovedEvent!;\n         await using (var command = new NpgsqlCommand(\n             \"\"\"\n@@ -296,6 +295,63 @@ INSERT INTO event_deliveries (event_id, consumer, available_at)\n         await transaction.CommitAsync(cancellationToken);\n     }\n \n+    public async Task StartDeployment(\n+        Promotion promotion,\n+        CancellationToken cancellationToken)\n+    {\n+        await using var connection = new NpgsqlConnection(connectionString);\n+        await connection.OpenAsync(cancellationToken);\n+        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);\n+        await using (var command = new NpgsqlCommand(\n+            \"\"\"\n+            UPDATE promotions\n+            SET status = 'deploying'\n+            WHERE id = $1 AND status = 'approved'\n+            \"\"\",\n+            connection,\n+            transaction))\n+        {\n+            command.Parameters.AddWithValue(promotion.Id.Value);\n+            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)\n+            {\n+                throw new InvalidPromotionTransition();\n+            }\n+        }\n+\n+        var domainEvent = promotion.StartedEvent!;\n+        await using (var command = new NpgsqlCommand(\n+            \"\"\"\n+            INSERT INTO domain_events (\n+                id, promotion_id, type, occurred_at, actor_id, payload\n+            )\n+            VALUES ($1, $2, 'deployment_started', $3, $4, '{}')\n+            \"\"\",\n+            connection,\n+            transaction))\n+        {\n+            command.Parameters.AddWithValue(domainEvent.Id.Value);\n+            command.Parameters.AddWithValue(domainEvent.PromotionId.Value);\n+            command.Parameters.AddWithValue(domainEvent.OccurredAt);\n+            command.Parameters.AddWithValue(domainEvent.ActorId.Value);\n+            await command.ExecuteNonQueryAsync(cancellationToken);\n+        }\n+\n+        await using (var command = new NpgsqlCommand(\n+            \"\"\"\n+            INSERT INTO event_deliveries (event_id, consumer, available_at)\n+            VALUES ($1, 'audit', $2)\n+            \"\"\",\n+            connection,\n+            transaction))\n+        {\n+            command.Parameters.AddWithValue(domainEvent.Id.Value);\n+            command.Parameters.AddWithValue(domainEvent.OccurredAt);\n+            await command.ExecuteNonQueryAsync(cancellationToken);\n+        }\n+\n+        await transaction.CommitAsync(cancellationToken);\n+    }\n+\n     public async Task<PromotionDetailsResponse?> Find(\n         Guid id,\n         CancellationToken cancellationToken)\n@@ -134,4 +134,46 @@ public void ApprovedPromotionCannotBeApprovedAgain()\n         Assert.Same(approvedEvent, promotion.ApprovedEvent);\n     }\n \n+    [Fact]\n+    public void StartsApprovedPromotionAndRecordsItsEvent()\n+    {\n+        var startedAt = DateTimeOffset.Parse(\"2026-07-23T12:10:00Z\");\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Approver,\n+            DateTimeOffset.Parse(\"2026-07-23T12:00:00Z\"));\n+        promotion.Approve(Approver, DateTimeOffset.Parse(\"2026-07-23T12:05:00Z\"));\n+\n+        promotion.StartDeployment(Actor, startedAt);\n+\n+        Assert.Equal(PromotionStatus.Deploying, promotion.Status);\n+        Assert.Equal(promotion.Id, promotion.StartedEvent!.PromotionId);\n+        Assert.Equal(Actor.Id, promotion.StartedEvent.ActorId);\n+        Assert.Equal(startedAt, promotion.StartedEvent.OccurredAt);\n+    }\n+\n+    [Fact]\n+    public void RequestedPromotionCannotStartDeployment()\n+    {\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Actor,\n+            DateTimeOffset.UtcNow);\n+\n+        var exception = Assert.Throws<InvalidPromotionTransition>(\n+            () => promotion.StartDeployment(Actor, DateTimeOffset.UtcNow));\n+\n+        Assert.Equal(\"invalid_promotion_transition\", exception.Code);\n+        Assert.Equal(PromotionStatus.Requested, promotion.Status);\n+        Assert.Null(promotion.StartedEvent);\n+    }\n+\n }\n@@ -3,7 +3,10 @@\n using System.Text.Json;\n using Microsoft.AspNetCore.Hosting;\n using Microsoft.AspNetCore.Mvc.Testing;\n+using Microsoft.Extensions.DependencyInjection;\n using Npgsql;\n+using ReleaseManagement.Domain;\n+using ReleaseManagement.Infrastructure;\n using Testcontainers.PostgreSql;\n \n namespace ReleasePilot.IntegrationTests;\n@@ -212,6 +215,76 @@ public async Task ApprovesRequestedPromotionWithControlledErrors()\n \n     }\n \n+    [Fact]\n+    public async Task StartsDeploymentIdempotentlyAndPreservesApprovedStateOnFailure()\n+    {\n+        client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000001\");\n+        var created = await client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new\n+            {\n+                applicationVersionId = \"01900000-0000-7000-8000-000000000201\",\n+                targetEnvironment = \"dev\"\n+            });\n+        var promotion = await created.Content.ReadFromJsonAsync<JsonElement>();\n+        var promotionId = promotion.GetProperty(\"id\").GetString();\n+        var approval = await client.PostAsync(\n+            $\"/promotions/{promotionId}/approve\",\n+            null);\n+        Assert.Equal(HttpStatusCode.NoContent, approval.StatusCode);\n+\n+        var deployment = application.Services.GetRequiredService<InMemoryDeploymentPort>();\n+        deployment.Unavailable = true;\n+\n+        var unavailable = await client.PostAsync(\n+            $\"/promotions/{promotionId}/start\",\n+            null);\n+\n+        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);\n+        var problem = await unavailable.Content.ReadFromJsonAsync<JsonElement>();\n+        Assert.Equal(\"deployment_unavailable\", problem.GetProperty(\"code\").GetString());\n+        Assert.DoesNotContain(\n+            \"Simulated\",\n+            problem.GetProperty(\"title\").GetString(),\n+            StringComparison.OrdinalIgnoreCase);\n+        var approved = await client.GetFromJsonAsync<JsonElement>(\n+            $\"/promotions/{promotionId}\");\n+        Assert.Equal(\"approved\", approved.GetProperty(\"status\").GetString());\n+        Assert.Equal(2, approved.GetProperty(\"history\").GetArrayLength());\n+\n+        deployment.Unavailable = false;\n+        var started = await client.PostAsync(\n+            $\"/promotions/{promotionId}/start\",\n+            null);\n+\n+        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);\n+        var details = await client.GetFromJsonAsync<JsonElement>(\n+            $\"/promotions/{promotionId}\");\n+        Assert.Equal(\"deploying\", details.GetProperty(\"status\").GetString());\n+        Assert.Equal(3, details.GetProperty(\"history\").GetArrayLength());\n+        Assert.Equal(\n+            \"deployment_started\",\n+            details.GetProperty(\"history\")[2].GetProperty(\"type\").GetString());\n+\n+        await deployment.Start(\n+            new PromotionId(Guid.Parse(promotionId!)),\n+            CancellationToken.None);\n+        Assert.Single(deployment.Requests);\n+\n+        var invalidTransition = await client.PostAsync(\n+            $\"/promotions/{promotionId}/start\",\n+            null);\n+        Assert.Equal(HttpStatusCode.Conflict, invalidTransition.StatusCode);\n+        Assert.Equal(\n+            \"invalid_promotion_transition\",\n+            (await invalidTransition.Content.ReadFromJsonAsync<JsonElement>())\n+                .GetProperty(\"code\")\n+                .GetString());\n+        Assert.Single(deployment.Requests);\n+    }\n+\n     public async Task DisposeAsync()\n     {\n         client.Dispose();",
>     "comments": [
>       {
>         "url": "https://github.com/jordialbert/ReleasePilot/pull/5#discussion_r3641358439",
>         "body": "**<sub><sub>![P2 Badge](https://img.shields.io/badge/P2-yellow?style=flat)</sub></sub>  Expose the documented start-deployment route**\n\nWhen API consumers follow the documented command list in `PLAN.md` (`POST /promotions/{id}/start-deployment`), this action is not matched because the new route is registered as `/promotions/{id}/start`, so approved promotions cannot be started through the planned public API. Rename the route segment (or add the documented alias) so the implementation matches the published HTTP surface.\n\nUseful? React with 👍 / 👎.",
>         "user": {
>           "login": "chatgpt-codex-connector[bot]",
>           "name": "chatgpt-codex-connector[bot]",
>           "email": null,
>           "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>           "id": 199175422
>         },
>         "id": 3641358439,
>         "pull_request_review_id": 4768162373,
>         "in_reply_to_id": null,
>         "created_at": "2026-07-23T20:59:58Z",
>         "updated_at": "2026-07-23T20:59:58Z",
>         "review": null,
>         "side": "RIGHT",
>         "line": 85,
>         "start_line": null,
>         "path": "apps/ReleasePilot/Api/Promotion/PromotionsController.cs",
>         "body_html": "<p dir=\"auto\"><strong><sub><sub><a target=\"_blank\" rel=\"noopener noreferrer nofollow\" href=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\"><img src=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\" alt=\"P2 Badge\" data-canonical-src=\"https://img.shields.io/badge/P2-yellow?style=flat\" style=\"max-width: 100%;\"></a></sub></sub>  Expose the documented start-deployment route</strong></p>\n<p dir=\"auto\">When API consumers follow the documented command list in <code class=\"notranslate\">PLAN.md</code> (<code class=\"notranslate\">POST /promotions/{id}/start-deployment</code>), this action is not matched because the new route is registered as <code class=\"notranslate\">/promotions/{id}/start</code>, so approved promotions cannot be started through the planned public API. Rename the route segment (or add the documented alias) so the implementation matches the published HTTP surface.</p>\n<p dir=\"auto\">Useful? React with 👍 / 👎.</p>"
>       },
>       {
>         "url": "https://github.com/jordialbert/ReleasePilot/pull/5#discussion_r3641358443",
>         "body": "**<sub><sub>![P2 Badge](https://img.shields.io/badge/P2-yellow?style=flat)</sub></sub>  Let deployment cancellations escape the wrapper**\n\nWhen the deployment adapter honors the supplied `cancellationToken` during a request abort or host shutdown, it will normally throw `OperationCanceledException`/`TaskCanceledException`, but this broad catch converts that into `deployment_unavailable` and reports an upstream outage instead of cancellation. Add a catch filter or rethrow cancellation exceptions so graceful shutdown and aborted requests are not misclassified as deployment-system failures.\n\nUseful? React with 👍 / 👎.",
>         "user": {
>           "login": "chatgpt-codex-connector[bot]",
>           "name": "chatgpt-codex-connector[bot]",
>           "email": null,
>           "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>           "id": 199175422
>         },
>         "id": 3641358443,
>         "pull_request_review_id": 4768162373,
>         "in_reply_to_id": null,
>         "created_at": "2026-07-23T20:59:58Z",
>         "updated_at": "2026-07-23T20:59:58Z",
>         "review": null,
>         "side": "RIGHT",
>         "line": 34,
>         "start_line": null,
>         "path": "src/ReleaseManagement/Promotion/Application/StartDeployment/StartDeploymentCommandHandler.cs",
>         "body_html": "<p dir=\"auto\"><strong><sub><sub><a target=\"_blank\" rel=\"noopener noreferrer nofollow\" href=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\"><img src=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\" alt=\"P2 Badge\" data-canonical-src=\"https://img.shields.io/badge/P2-yellow?style=flat\" style=\"max-width: 100%;\"></a></sub></sub>  Let deployment cancellations escape the wrapper</strong></p>\n<p dir=\"auto\">When the deployment adapter honors the supplied <code class=\"notranslate\">cancellationToken</code> during a request abort or host shutdown, it will normally throw <code class=\"notranslate\">OperationCanceledException</code>/<code class=\"notranslate\">TaskCanceledException</code>, but this broad catch converts that into <code class=\"notranslate\">deployment_unavailable</code> and reports an upstream outage instead of cancellation. Add a catch filter or rethrow cancellation exceptions so graceful shutdown and aborted requests are not misclassified as deployment-system failures.</p>\n<p dir=\"auto\">Useful? React with 👍 / 👎.</p>"
>       },
>       {
>         "url": "https://github.com/jordialbert/ReleasePilot/pull/5#pullrequestreview-4768162373",
>         "body": "\n### 💡 Codex Review\n\nHere are some automated review suggestions for this pull request.\n\n**Reviewed commit:** `0e0f6619f2`\n    \n\n<details> <summary>ℹ️ About Codex in GitHub</summary>\n<br/>\n\n[Your team has set up Codex to review pull requests in this repo](https://chatgpt.com/codex/cloud/settings/general). Reviews are triggered when you\n- Open a pull request for review\n- Mark a draft as ready\n- Comment \"@codex review\".\n\nIf Codex has suggestions, it will comment; otherwise it will react with 👍.\n\n\n\n\nCodex can also answer questions or update the PR. Try commenting \"@codex address that feedback\".\n            \n</details>",
>         "user": {
>           "login": "chatgpt-codex-connector[bot]",
>           "name": "chatgpt-codex-connector[bot]",
>           "email": null,
>           "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>           "id": 199175422
>         },
>         "id": 4768162373,
>         "pull_request_review_id": null,
>         "in_reply_to_id": null,
>         "created_at": "2026-07-23T20:59:58Z",
>         "updated_at": null,
>         "review": "COMMENTED",
>         "side": null,
>         "line": null,
>         "start_line": null,
>         "path": null,
>         "body_html": "<h3 dir=\"auto\">💡 Codex Review</h3>\n<p dir=\"auto\">Here are some automated review suggestions for this pull request.</p>\n<p dir=\"auto\"><strong>Reviewed commit:</strong> <code class=\"notranslate\">0e0f6619f2</code></p>\n<details> <summary>ℹ️ About Codex in GitHub</summary>\n<br>\n<p dir=\"auto\"><a href=\"https://chatgpt.com/codex/cloud/settings/general\" rel=\"nofollow\">Your team has set up Codex to review pull requests in this repo</a>. Reviews are triggered when you</p>\n<ul dir=\"auto\">\n<li>Open a pull request for review</li>\n<li>Mark a draft as ready</li>\n<li>Comment \"<a class=\"user-mention notranslate\" data-hovercard-type=\"user\" data-hovercard-url=\"/users/codex/hovercard\" data-octo-click=\"hovercard-link-click\" data-octo-dimensions=\"link_type:self\" href=\"https://github.com/codex\">@codex</a> review\".</li>\n</ul>\n<p dir=\"auto\">If Codex has suggestions, it will comment; otherwise it will react with 👍.</p>\n<p dir=\"auto\">Codex can also answer questions or update the PR. Try commenting \"<a class=\"user-mention notranslate\" data-hovercard-type=\"user\" data-hovercard-url=\"/users/codex/hovercard\" data-octo-click=\"hovercard-link-click\" data-octo-dimensions=\"link_type:self\" href=\"https://github.com/codex\">@codex</a> address that feedback\".</p>\n</details>"
>       }
>     ],
>     "created_at": "2026-07-23T20:57:18Z",
>     "updated_at": "2026-07-23T20:59:58Z",
>     "closed_at": null,
>     "merged_at": null,
>     "commits": 1,
>     "changed_files": 14,
>     "additions": 322,
>     "deletions": 1,
>     "display_url": "https://github.com/jordialbert/ReleasePilot/pull/5",
>     "display_title": "feat: start Deployment for Approved Promotions"
>   },
>   "url": "https://github.com/jordialbert/ReleasePilot/pull/5",
>   "title": "feat: start Deployment for Approved Promotions",
>   "diff": "@@ -36,8 +36,12 @@\n     services => services.GetRequiredService<PostgreSqlPromotionRepository>());\n builder.Services.AddSingleton<IPromotionDetailsReader>(\n     services => services.GetRequiredService<PostgreSqlPromotionRepository>());\n+builder.Services.AddSingleton<InMemoryDeploymentPort>();\n+builder.Services.AddSingleton<IDeploymentPort>(\n+    services => services.GetRequiredService<InMemoryDeploymentPort>());\n builder.Services.AddScoped<RequestPromotionCommandHandler>();\n builder.Services.AddScoped<ApprovePromotionCommandHandler>();\n+builder.Services.AddScoped<StartDeploymentCommandHandler>();\n builder.Services.AddScoped<GetPromotionDetailsQueryHandler>();\n builder.Services\n     .AddHealthChecks()\n@@ -8,6 +8,7 @@ namespace ReleasePilot.Api.Promotion;\n public sealed class PromotionsController(\n     RequestPromotionCommandHandler requestPromotion,\n     ApprovePromotionCommandHandler approvePromotion,\n+    StartDeploymentCommandHandler startDeployment,\n     GetPromotionDetailsQueryHandler getPromotionDetails) : ControllerBase\n {\n     [HttpPost]\n@@ -81,6 +82,40 @@ await approvePromotion.Handle(\n         return NoContent();\n     }\n \n+    [HttpPost(\"{id}/start\")]\n+    public async Task<IActionResult> StartDeployment(\n+        string id,\n+        [FromHeader(Name = \"X-User-Id\")] string? actorHeader,\n+        CancellationToken cancellationToken)\n+    {\n+        if (actorHeader is null)\n+        {\n+            var problem = new ProblemDetails\n+            {\n+                Status = StatusCodes.Status401Unauthorized,\n+                Title = \"The X-User-Id header is required.\"\n+            };\n+            problem.Extensions[\"code\"] = \"missing_actor\";\n+            problem.Extensions[\"traceId\"] = HttpContext.TraceIdentifier;\n+            return Unauthorized(problem);\n+        }\n+\n+        if (!Guid.TryParse(actorHeader, out var actorId))\n+        {\n+            throw new InvalidInput(\"X-User-Id\");\n+        }\n+\n+        if (!Guid.TryParse(id, out var promotionId))\n+        {\n+            throw new InvalidInput(\"id\");\n+        }\n+\n+        await startDeployment.Handle(\n+            new StartDeploymentCommand(promotionId, actorId),\n+            cancellationToken);\n+        return NoContent();\n+    }\n+\n     [HttpGet(\"{id}\")]\n     public async Task<ActionResult<PromotionDetailsResponse>> GetPromotionDetails(\n         string id,\n@@ -37,6 +37,11 @@ public async ValueTask<bool> TryHandleAsync(\n                 code = resourceNotFound.Code;\n                 title = resourceNotFound.Message;\n                 break;\n+            case DeploymentUnavailable deploymentUnavailable:\n+                status = StatusCodes.Status503ServiceUnavailable;\n+                code = deploymentUnavailable.Code;\n+                title = deploymentUnavailable.Message;\n+                break;\n             case OnlyApproverCanApprove onlyApproverCanApprove:\n                 status = StatusCodes.Status403Forbidden;\n                 code = onlyApproverCanApprove.Code;\n@@ -0,0 +1,7 @@\n+namespace ReleaseManagement.Application;\n+\n+public sealed class DeploymentUnavailable()\n+    : Exception(\"The Deployment system is unavailable.\")\n+{\n+    public string Code => \"deployment_unavailable\";\n+}\n@@ -0,0 +1,5 @@\n+namespace ReleaseManagement.Application;\n+\n+public sealed record StartDeploymentCommand(\n+    Guid PromotionId,\n+    Guid ActorId);\n@@ -0,0 +1,41 @@\n+using ReleaseManagement.Domain;\n+\n+namespace ReleaseManagement.Application;\n+\n+public sealed class StartDeploymentCommandHandler(\n+    IPromotionRepository promotions,\n+    IDeploymentPort deployment)\n+{\n+    public async Task Handle(\n+        StartDeploymentCommand command,\n+        CancellationToken cancellationToken)\n+    {\n+        var actor = await promotions.FindActor(\n+            new UserId(command.ActorId),\n+            cancellationToken);\n+        if (actor is null)\n+        {\n+            throw new UnknownActor();\n+        }\n+\n+        var promotion = await promotions.Find(\n+            new PromotionId(command.PromotionId),\n+            cancellationToken);\n+        if (promotion is null)\n+        {\n+            throw new ResourceNotFound(\"promotion\");\n+        }\n+\n+        promotion.StartDeployment(actor, DateTimeOffset.UtcNow);\n+        try\n+        {\n+            await deployment.Start(promotion.Id, cancellationToken);\n+        }\n+        catch (Exception)\n+        {\n+            throw new DeploymentUnavailable();\n+        }\n+\n+        await promotions.StartDeployment(promotion, cancellationToken);\n+    }\n+}\n@@ -0,0 +1,7 @@\n+namespace ReleaseManagement.Domain;\n+\n+public sealed record DeploymentStarted(\n+    DomainEventId Id,\n+    PromotionId PromotionId,\n+    DateTimeOffset OccurredAt,\n+    UserId ActorId);\n@@ -0,0 +1,6 @@\n+namespace ReleaseManagement.Domain;\n+\n+public interface IDeploymentPort\n+{\n+    Task Start(PromotionId promotionId, CancellationToken cancellationToken);\n+}\n@@ -16,4 +16,5 @@ Task<bool> HasActivePromotion(\n     Task<Promotion?> Find(PromotionId id, CancellationToken cancellationToken);\n     Task Add(Promotion promotion, CancellationToken cancellationToken);\n     Task Update(Promotion promotion, CancellationToken cancellationToken);\n+    Task StartDeployment(Promotion promotion, CancellationToken cancellationToken);\n }\n@@ -54,6 +54,7 @@ internal Promotion(\n     public DateTimeOffset RequestedAt { get; }\n     public PromotionRequested? RequestedEvent { get; }\n     public PromotionApproved? ApprovedEvent { get; private set; }\n+    public DeploymentStarted? StartedEvent { get; private set; }\n \n     public static Promotion Request(\n         PromotionId id,\n@@ -123,4 +124,19 @@ public void Approve(Actor actor, DateTimeOffset approvedAt)\n             approvedAt,\n             actor.Id);\n     }\n+\n+    public void StartDeployment(Actor actor, DateTimeOffset startedAt)\n+    {\n+        if (Status != PromotionStatus.Approved)\n+        {\n+            throw new InvalidPromotionTransition();\n+        }\n+\n+        Status = PromotionStatus.Deploying;\n+        StartedEvent = new DeploymentStarted(\n+            new DomainEventId(Guid.CreateVersion7()),\n+            Id,\n+            startedAt,\n+            actor.Id);\n+    }\n }\n@@ -0,0 +1,23 @@\n+using System.Collections.Concurrent;\n+using ReleaseManagement.Domain;\n+\n+namespace ReleaseManagement.Infrastructure;\n+\n+public sealed class InMemoryDeploymentPort : IDeploymentPort\n+{\n+    private readonly ConcurrentDictionary<PromotionId, byte> requests = new();\n+\n+    public bool Unavailable { get; set; }\n+    public ICollection<PromotionId> Requests => requests.Keys;\n+\n+    public Task Start(PromotionId promotionId, CancellationToken cancellationToken)\n+    {\n+        if (Unavailable)\n+        {\n+            throw new Exception(\"Simulated Deployment system failure.\");\n+        }\n+\n+        requests.TryAdd(promotionId, 0);\n+        return Task.CompletedTask;\n+    }\n+}\n@@ -259,7 +259,6 @@ UPDATE promotions\n                 throw new InvalidPromotionTransition();\n             }\n         }\n-\n         var domainEvent = promotion.ApprovedEvent!;\n         await using (var command = new NpgsqlCommand(\n             \"\"\"\n@@ -296,6 +295,63 @@ INSERT INTO event_deliveries (event_id, consumer, available_at)\n         await transaction.CommitAsync(cancellationToken);\n     }\n \n+    public async Task StartDeployment(\n+        Promotion promotion,\n+        CancellationToken cancellationToken)\n+    {\n+        await using var connection = new NpgsqlConnection(connectionString);\n+        await connection.OpenAsync(cancellationToken);\n+        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);\n+        await using (var command = new NpgsqlCommand(\n+            \"\"\"\n+            UPDATE promotions\n+            SET status = 'deploying'\n+            WHERE id = $1 AND status = 'approved'\n+            \"\"\",\n+            connection,\n+            transaction))\n+        {\n+            command.Parameters.AddWithValue(promotion.Id.Value);\n+            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)\n+            {\n+                throw new InvalidPromotionTransition();\n+            }\n+        }\n+\n+        var domainEvent = promotion.StartedEvent!;\n+        await using (var command = new NpgsqlCommand(\n+            \"\"\"\n+            INSERT INTO domain_events (\n+                id, promotion_id, type, occurred_at, actor_id, payload\n+            )\n+            VALUES ($1, $2, 'deployment_started', $3, $4, '{}')\n+            \"\"\",\n+            connection,\n+            transaction))\n+        {\n+            command.Parameters.AddWithValue(domainEvent.Id.Value);\n+            command.Parameters.AddWithValue(domainEvent.PromotionId.Value);\n+            command.Parameters.AddWithValue(domainEvent.OccurredAt);\n+            command.Parameters.AddWithValue(domainEvent.ActorId.Value);\n+            await command.ExecuteNonQueryAsync(cancellationToken);\n+        }\n+\n+        await using (var command = new NpgsqlCommand(\n+            \"\"\"\n+            INSERT INTO event_deliveries (event_id, consumer, available_at)\n+            VALUES ($1, 'audit', $2)\n+            \"\"\",\n+            connection,\n+            transaction))\n+        {\n+            command.Parameters.AddWithValue(domainEvent.Id.Value);\n+            command.Parameters.AddWithValue(domainEvent.OccurredAt);\n+            await command.ExecuteNonQueryAsync(cancellationToken);\n+        }\n+\n+        await transaction.CommitAsync(cancellationToken);\n+    }\n+\n     public async Task<PromotionDetailsResponse?> Find(\n         Guid id,\n         CancellationToken cancellationToken)\n@@ -134,4 +134,46 @@ public void ApprovedPromotionCannotBeApprovedAgain()\n         Assert.Same(approvedEvent, promotion.ApprovedEvent);\n     }\n \n+    [Fact]\n+    public void StartsApprovedPromotionAndRecordsItsEvent()\n+    {\n+        var startedAt = DateTimeOffset.Parse(\"2026-07-23T12:10:00Z\");\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Approver,\n+            DateTimeOffset.Parse(\"2026-07-23T12:00:00Z\"));\n+        promotion.Approve(Approver, DateTimeOffset.Parse(\"2026-07-23T12:05:00Z\"));\n+\n+        promotion.StartDeployment(Actor, startedAt);\n+\n+        Assert.Equal(PromotionStatus.Deploying, promotion.Status);\n+        Assert.Equal(promotion.Id, promotion.StartedEvent!.PromotionId);\n+        Assert.Equal(Actor.Id, promotion.StartedEvent.ActorId);\n+        Assert.Equal(startedAt, promotion.StartedEvent.OccurredAt);\n+    }\n+\n+    [Fact]\n+    public void RequestedPromotionCannotStartDeployment()\n+    {\n+        var promotion = Promotion.Request(\n+            new PromotionId(Guid.CreateVersion7()),\n+            Version,\n+            DeploymentEnvironment.Dev,\n+            null,\n+            false,\n+            Actor,\n+            DateTimeOffset.UtcNow);\n+\n+        var exception = Assert.Throws<InvalidPromotionTransition>(\n+            () => promotion.StartDeployment(Actor, DateTimeOffset.UtcNow));\n+\n+        Assert.Equal(\"invalid_promotion_transition\", exception.Code);\n+        Assert.Equal(PromotionStatus.Requested, promotion.Status);\n+        Assert.Null(promotion.StartedEvent);\n+    }\n+\n }\n@@ -3,7 +3,10 @@\n using System.Text.Json;\n using Microsoft.AspNetCore.Hosting;\n using Microsoft.AspNetCore.Mvc.Testing;\n+using Microsoft.Extensions.DependencyInjection;\n using Npgsql;\n+using ReleaseManagement.Domain;\n+using ReleaseManagement.Infrastructure;\n using Testcontainers.PostgreSql;\n \n namespace ReleasePilot.IntegrationTests;\n@@ -212,6 +215,76 @@ public async Task ApprovesRequestedPromotionWithControlledErrors()\n \n     }\n \n+    [Fact]\n+    public async Task StartsDeploymentIdempotentlyAndPreservesApprovedStateOnFailure()\n+    {\n+        client.DefaultRequestHeaders.Add(\n+            \"X-User-Id\",\n+            \"01900000-0000-7000-8000-000000000001\");\n+        var created = await client.PostAsJsonAsync(\n+            \"/promotions\",\n+            new\n+            {\n+                applicationVersionId = \"01900000-0000-7000-8000-000000000201\",\n+                targetEnvironment = \"dev\"\n+            });\n+        var promotion = await created.Content.ReadFromJsonAsync<JsonElement>();\n+        var promotionId = promotion.GetProperty(\"id\").GetString();\n+        var approval = await client.PostAsync(\n+            $\"/promotions/{promotionId}/approve\",\n+            null);\n+        Assert.Equal(HttpStatusCode.NoContent, approval.StatusCode);\n+\n+        var deployment = application.Services.GetRequiredService<InMemoryDeploymentPort>();\n+        deployment.Unavailable = true;\n+\n+        var unavailable = await client.PostAsync(\n+            $\"/promotions/{promotionId}/start\",\n+            null);\n+\n+        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);\n+        var problem = await unavailable.Content.ReadFromJsonAsync<JsonElement>();\n+        Assert.Equal(\"deployment_unavailable\", problem.GetProperty(\"code\").GetString());\n+        Assert.DoesNotContain(\n+            \"Simulated\",\n+            problem.GetProperty(\"title\").GetString(),\n+            StringComparison.OrdinalIgnoreCase);\n+        var approved = await client.GetFromJsonAsync<JsonElement>(\n+            $\"/promotions/{promotionId}\");\n+        Assert.Equal(\"approved\", approved.GetProperty(\"status\").GetString());\n+        Assert.Equal(2, approved.GetProperty(\"history\").GetArrayLength());\n+\n+        deployment.Unavailable = false;\n+        var started = await client.PostAsync(\n+            $\"/promotions/{promotionId}/start\",\n+            null);\n+\n+        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);\n+        var details = await client.GetFromJsonAsync<JsonElement>(\n+            $\"/promotions/{promotionId}\");\n+        Assert.Equal(\"deploying\", details.GetProperty(\"status\").GetString());\n+        Assert.Equal(3, details.GetProperty(\"history\").GetArrayLength());\n+        Assert.Equal(\n+            \"deployment_started\",\n+            details.GetProperty(\"history\")[2].GetProperty(\"type\").GetString());\n+\n+        await deployment.Start(\n+            new PromotionId(Guid.Parse(promotionId!)),\n+            CancellationToken.None);\n+        Assert.Single(deployment.Requests);\n+\n+        var invalidTransition = await client.PostAsync(\n+            $\"/promotions/{promotionId}/start\",\n+            null);\n+        Assert.Equal(HttpStatusCode.Conflict, invalidTransition.StatusCode);\n+        Assert.Equal(\n+            \"invalid_promotion_transition\",\n+            (await invalidTransition.Content.ReadFromJsonAsync<JsonElement>())\n+                .GetProperty(\"code\")\n+                .GetString());\n+        Assert.Single(deployment.Requests);\n+    }\n+\n     public async Task DisposeAsync()\n     {\n         client.Dispose();",
>   "comments": [
>     {
>       "url": "https://github.com/jordialbert/ReleasePilot/pull/5#discussion_r3641358439",
>       "body": "**<sub><sub>![P2 Badge](https://img.shields.io/badge/P2-yellow?style=flat)</sub></sub>  Expose the documented start-deployment route**\n\nWhen API consumers follow the documented command list in `PLAN.md` (`POST /promotions/{id}/start-deployment`), this action is not matched because the new route is registered as `/promotions/{id}/start`, so approved promotions cannot be started through the planned public API. Rename the route segment (or add the documented alias) so the implementation matches the published HTTP surface.\n\nUseful? React with 👍 / 👎.",
>       "user": {
>         "login": "chatgpt-codex-connector[bot]",
>         "name": "chatgpt-codex-connector[bot]",
>         "email": null,
>         "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>         "id": 199175422
>       },
>       "id": 3641358439,
>       "pull_request_review_id": 4768162373,
>       "in_reply_to_id": null,
>       "created_at": "2026-07-23T20:59:58Z",
>       "updated_at": "2026-07-23T20:59:58Z",
>       "review": null,
>       "side": "RIGHT",
>       "line": 85,
>       "start_line": null,
>       "path": "apps/ReleasePilot/Api/Promotion/PromotionsController.cs",
>       "body_html": "<p dir=\"auto\"><strong><sub><sub><a target=\"_blank\" rel=\"noopener noreferrer nofollow\" href=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\"><img src=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\" alt=\"P2 Badge\" data-canonical-src=\"https://img.shields.io/badge/P2-yellow?style=flat\" style=\"max-width: 100%;\"></a></sub></sub>  Expose the documented start-deployment route</strong></p>\n<p dir=\"auto\">When API consumers follow the documented command list in <code class=\"notranslate\">PLAN.md</code> (<code class=\"notranslate\">POST /promotions/{id}/start-deployment</code>), this action is not matched because the new route is registered as <code class=\"notranslate\">/promotions/{id}/start</code>, so approved promotions cannot be started through the planned public API. Rename the route segment (or add the documented alias) so the implementation matches the published HTTP surface.</p>\n<p dir=\"auto\">Useful? React with 👍 / 👎.</p>"
>     },
>     {
>       "url": "https://github.com/jordialbert/ReleasePilot/pull/5#discussion_r3641358443",
>       "body": "**<sub><sub>![P2 Badge](https://img.shields.io/badge/P2-yellow?style=flat)</sub></sub>  Let deployment cancellations escape the wrapper**\n\nWhen the deployment adapter honors the supplied `cancellationToken` during a request abort or host shutdown, it will normally throw `OperationCanceledException`/`TaskCanceledException`, but this broad catch converts that into `deployment_unavailable` and reports an upstream outage instead of cancellation. Add a catch filter or rethrow cancellation exceptions so graceful shutdown and aborted requests are not misclassified as deployment-system failures.\n\nUseful? React with 👍 / 👎.",
>       "user": {
>         "login": "chatgpt-codex-connector[bot]",
>         "name": "chatgpt-codex-connector[bot]",
>         "email": null,
>         "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>         "id": 199175422
>       },
>       "id": 3641358443,
>       "pull_request_review_id": 4768162373,
>       "in_reply_to_id": null,
>       "created_at": "2026-07-23T20:59:58Z",
>       "updated_at": "2026-07-23T20:59:58Z",
>       "review": null,
>       "side": "RIGHT",
>       "line": 34,
>       "start_line": null,
>       "path": "src/ReleaseManagement/Promotion/Application/StartDeployment/StartDeploymentCommandHandler.cs",
>       "body_html": "<p dir=\"auto\"><strong><sub><sub><a target=\"_blank\" rel=\"noopener noreferrer nofollow\" href=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\"><img src=\"https://camo.githubusercontent.com/f2c1aacb361ddd3a0e9f9cacdb84fab050de434017f6747bb916e31e29bdf03d/68747470733a2f2f696d672e736869656c64732e696f2f62616467652f50322d79656c6c6f773f7374796c653d666c6174\" alt=\"P2 Badge\" data-canonical-src=\"https://img.shields.io/badge/P2-yellow?style=flat\" style=\"max-width: 100%;\"></a></sub></sub>  Let deployment cancellations escape the wrapper</strong></p>\n<p dir=\"auto\">When the deployment adapter honors the supplied <code class=\"notranslate\">cancellationToken</code> during a request abort or host shutdown, it will normally throw <code class=\"notranslate\">OperationCanceledException</code>/<code class=\"notranslate\">TaskCanceledException</code>, but this broad catch converts that into <code class=\"notranslate\">deployment_unavailable</code> and reports an upstream outage instead of cancellation. Add a catch filter or rethrow cancellation exceptions so graceful shutdown and aborted requests are not misclassified as deployment-system failures.</p>\n<p dir=\"auto\">Useful? React with 👍 / 👎.</p>"
>     },
>     {
>       "url": "https://github.com/jordialbert/ReleasePilot/pull/5#pullrequestreview-4768162373",
>       "body": "\n### 💡 Codex Review\n\nHere are some automated review suggestions for this pull request.\n\n**Reviewed commit:** `0e0f6619f2`\n    \n\n<details> <summary>ℹ️ About Codex in GitHub</summary>\n<br/>\n\n[Your team has set up Codex to review pull requests in this repo](https://chatgpt.com/codex/cloud/settings/general). Reviews are triggered when you\n- Open a pull request for review\n- Mark a draft as ready\n- Comment \"@codex review\".\n\nIf Codex has suggestions, it will comment; otherwise it will react with 👍.\n\n\n\n\nCodex can also answer questions or update the PR. Try commenting \"@codex address that feedback\".\n            \n</details>",
>       "user": {
>         "login": "chatgpt-codex-connector[bot]",
>         "name": "chatgpt-codex-connector[bot]",
>         "email": null,
>         "avatar_url": "https://avatars.githubusercontent.com/in/1144995?v=4",
>         "id": 199175422
>       },
>       "id": 4768162373,
>       "pull_request_review_id": null,
>       "in_reply_to_id": null,
>       "created_at": "2026-07-23T20:59:58Z",
>       "updated_at": null,
>       "review": "COMMENTED",
>       "side": null,
>       "line": null,
>       "start_line": null,
>       "path": null,
>       "body_html": "<h3 dir=\"auto\">💡 Codex Review</h3>\n<p dir=\"auto\">Here are some automated review suggestions for this pull request.</p>\n<p dir=\"auto\"><strong>Reviewed commit:</strong> <code class=\"notranslate\">0e0f6619f2</code></p>\n<details> <summary>ℹ️ About Codex in GitHub</summary>\n<br>\n<p dir=\"auto\"><a href=\"https://chatgpt.com/codex/cloud/settings/general\" rel=\"nofollow\">Your team has set up Codex to review pull requests in this repo</a>. Reviews are triggered when you</p>\n<ul dir=\"auto\">\n<li>Open a pull request for review</li>\n<li>Mark a draft as ready</li>\n<li>Comment \"<a class=\"user-mention notranslate\" data-hovercard-type=\"user\" data-hovercard-url=\"/users/codex/hovercard\" data-octo-click=\"hovercard-link-click\" data-octo-dimensions=\"link_type:self\" href=\"https://github.com/codex\">@codex</a> review\".</li>\n</ul>\n<p dir=\"auto\">If Codex has suggestions, it will comment; otherwise it will react with 👍.</p>\n<p dir=\"auto\">Codex can also answer questions or update the PR. Try commenting \"<a class=\"user-mention notranslate\" data-hovercard-type=\"user\" data-hovercard-url=\"/users/codex/hovercard\" data-octo-click=\"hovercard-link-click\" data-octo-dimensions=\"link_type:self\" href=\"https://github.com/codex\">@codex</a> address that feedback\".</p>\n</details>"
>     }
>   ],
>   "display_url": "https://github.com/jordialbert/ReleasePilot/pull/5",
>   "display_title": "feat: start Deployment for Approved Promotions"
> }
> ```
>
> </details>
>
> There are exactly two unresolved actionable threads:
>
> 1. Rename the public route from `/start` to the documented `/start-deployment`—no compatibility alias.
> 2. Let request/host cancellation escape the broad deployment-failure wrapper while continuing to map genuine adapter failures to `503`.
>
> Both are valid and non-conflicting. I’m applying those two minimal changes and updating the focused integration scenario to exercise the documented route.
>
> <details><summary>Edited 3 files</summary>
>
> - Wrote `./apps/ReleasePilot/Api/Promotion/PromotionsController.cs` (+1 -1)
> - Wrote `./src/ReleaseManagement/Promotion/Application/StartDeployment/StartDeploymentCommandHandler.cs` (+1 -1)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/RequestPromotionTests.cs` (+3 -3)
>
> </details>
>
> The two code fixes are in place. The outage path still uses the existing broad failure mapping, but only when the supplied cancellation token has not been cancelled; request and shutdown cancellation now propagates. I’m running the focused Promotion integration class and diff checks before committing.
>
> The focused integration class passes, and the review fixes are committed as `aca6380`. I’m pushing that commit and correcting the PR description to name the documented `/start-deployment` route. I will not resolve or reply to the review threads without a separate explicit request.
>
> <details><summary>Called a tool</summary>
>
> MCP tool call
>
> codex_apps.github.update_pull_request
>
> ```json
> {
>   "repository_full_name": "jordialbert/ReleasePilot",
>   "pr_number": 5,
>   "body": "## Summary\n\n- add `POST /promotions/{id}/start-deployment` for successful `204 No Content` transitions\n- invoke an idempotent in-memory Deployment adapter using the Promotion ID\n- persist the Deploying snapshot and exactly one `DeploymentStarted` event atomically\n- return safe `503 deployment_unavailable` Problem Details without persisting failed starts\n\n## Why\n\nTicket 4 requires Approved Promotions to start external Deployment without duplicate execution or recording a transition when the external system is unavailable.\n\n## Impact\n\nRelease managers can start Approved Promotions through the documented public API. Invalid lifecycle transitions remain controlled conflicts, repeated external requests are deduplicated, and Deployment failures leave the stored Promotion Approved with unchanged history.\n\n## Verification\n\n- `dotnet test tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj --no-restore`\n- `dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj --filter FullyQualifiedName~RequestPromotionTests --no-restore`\n- `git diff --check`"
> }
> ```
>
> ```text
> Action completed.
> ```
>
> ```json
> {
>   "url": "https://github.com/jordialbert/ReleasePilot/pull/5",
>   "number": 5,
>   "state": "open",
>   "merged": false,
>   "mergeable": true,
>   "draft": false,
>   "body": "## Summary\n\n- add `POST /promotions/{id}/start-deployment` for successful `204 No Content` transitions\n- invoke an idempotent in-memory Deployment adapter using the Promotion ID\n- persist the Deploying snapshot and exactly one `DeploymentStarted` event atomically\n- return safe `503 deployment_unavailable` Problem Details without persisting failed starts\n\n## Why\n\nTicket 4 requires Approved Promotions to start external Deployment without duplicate execution or recording a transition when the external system is unavailable.\n\n## Impact\n\nRelease managers can start Approved Promotions through the documented public API. Invalid lifecycle transitions remain controlled conflicts, repeated external requests are deduplicated, and Deployment failures leave the stored Promotion Approved with unchanged history.\n\n## Verification\n\n- `dotnet test tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj --no-restore`\n- `dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj --filter FullyQualifiedName~RequestPromotionTests --no-restore`\n- `git diff --check`",
>   "title": "feat: start Deployment for Approved Promotions",
>   "base": "main",
>   "base_sha": "bf9b5ca509285bc1e8c40ab7702f5cc5da668243",
>   "head": "feat/start-deployment",
>   "head_sha": "aca6380638c67b2c32044b039ccafcad0d0af7d7",
>   "merge_commit_sha": "37d6fc7f8420a07b79263fc8f7e298a60d6972e9",
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
>   "created_at": "2026-07-23T20:57:18Z",
>   "updated_at": "2026-07-23T21:03:18Z",
>   "closed_at": null,
>   "merged_at": null,
>   "commits": 2,
>   "changed_files": 14,
>   "additions": 322,
>   "deletions": 1,
>   "display_url": "https://github.com/jordialbert/ReleasePilot/pull/5",
>   "display_title": "feat: start Deployment for Approved Promotions"
> }
> ```
>
> </details>
</details>

Addressed both PR #5 comments and pushed commit `aca6380`.

Changes:

- Public route is now `POST /promotions/{id}/start-deployment`.
- Request/host cancellations escape the deployment-unavailable wrapper.
- Integration tests use the documented route.
- PR description was corrected.

Verification:

- Promotion integration tests: 3/3 passed
- `git diff --check` passed
- PR remains ready, open, and cleanly mergeable.

Both review threads are now outdated but remain unresolved because resolving/replying was not explicitly requested.

[View PR #5](https://github.com/jordialbert/ReleasePilot/pull/5)
