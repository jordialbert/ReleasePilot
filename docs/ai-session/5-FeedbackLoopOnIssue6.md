# Unify promotion concurrency model

> analyze this feedback for the PR and local ccode changes for the current branch and apply if it makes sense:
>
> User context
>
> Attachments:
> - All relevant tests pass (18 domain tests, 11 integration tests covering cancel,…: `~/.codex/attachments/d52ec1ce-bf34-4dc8-9f5d-135b8109bc08/pasted-text.txt`

<details><summary>6 previous messages</summary>

> I’m using the code-review skill to compare the PR feedback against the actual branch diff, then I’ll apply only the feedback that is technically justified and run the smallest focused test set.
>
> The review skill explicitly calls for two independent checks here, so I’m splitting the assessment into standards and spec axes while I inspect the concurrency design and tests locally. I’m treating `origin/main` as the fixed point and including the uncommitted worktree changes because the feedback explicitly covers both.
>
> The central feedback is substantively correct: the row-locked transaction is safer than the advisory-lock version, and leaving approve/complete on the old optimistic path would preserve two persistence models for the same aggregate. I’m checking the smallest way to finish that migration without introducing a new helper type, in line with this repository’s instructions.
>
> The independent spec check confirms the cancel feature is complete. I’m not taking the proposed approve/complete migration: those are database-only transitions where optimistic concurrency remains appropriate, while cancel/start uniquely require serialization because deployment start has an external side effect. Expanding the row-lock transaction to every transition would alter established behavior and exceed the issue’s scope. I am applying the two concrete fixes: correct the impossible-event invariant handling and make the lock-wait test bounded, paced, and specific to the blocked transition query.
>
> <details><summary>Edited 2 files</summary>
>
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs` (+1 -5)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+10 -3)
>
> </details>
>
> The focused integration suite is green: all 11 cancel, start-deployment, and completion/concurrency tests passed. I’m doing the final diff check now, including confirming that no unrelated user changes were touched.
</details>

Applied the feedback that fits the PR’s scope:

- Removed the misleading `InvalidPromotionTransition` branch and used the aggregate invariant consistently in [PostgreSqlPromotionRepository.cs](./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs:101).
- Hardened the concurrency test with a 10-second bound, polling delay, and a predicate scoped to blocked promotion `FOR UPDATE` queries in [CancelPromotionTests.cs](./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs:186).

I did not apply findings #1–2:

- Approve/complete are database-only transitions where optimistic concurrency remains appropriate.
- Cancel/start require serialization specifically because deployment start has an external side effect.
- Migrating everything would alter established concurrency behavior and expand issue 06’s scope.
- Adding another shared helper conflicts with the repository’s explicit “Don’t add helpers” instruction.

The spec review found no missing cancellation requirements. One separate standards caveat remains: ADR 0005 expects handlers to delegate to named application use cases, while the changed handlers orchestrate directly. Addressing that properly would be a separate architectural refactor, not the suggested helper.

Verification: 11/11 focused integration tests passed; `git diff --check` passed.

> why #2 is not ok?

#2 isn’t technically wrong; I rejected the proposed solution because it doesn’t fit this repository.

The duplicated code is only two small boundary operations:

- Resolve the actor or throw `UnknownActor`.
- Convert the repository’s `false` result into `ResourceNotFound`.

A generic `PromotionTransition.Execute(...)` would centralize them, but it would also:

- Conflict with the explicit “Don’t add helpers” instruction.
- Introduce a callback-based abstraction for only two callers.
- Hide important orchestration—especially that start-deployment performs an external call while holding a database transaction.
- Act like a generic use case, while ADR 0005 asks for named application use cases.

The feedback assumes approve and complete will also migrate to `Transition`, creating four duplicated handlers. Because I rejected that migration, the main justification for the helper disappears.

So my position is: the duplication observation is valid, but the proposed helper is not worthwhile under the chosen concurrency design. If consolidation is desired, named `CancelPromotion` and `StartDeployment` application use cases would align better with the ADR than a generic transition helper.

> Wht do you mean by that?
>
> > If consolidation is desired, named CancelPromotion and StartDeployment application use cases would align better with the ADR than a generic transition helper

I mean separating the CQRS handler from the actual workflow, as ADR 0005 requests.

Today the handler performs everything:

```csharp
public Task Handle(CancelPromotionCommand command, CancellationToken ct)
{
    // Find actor
    // Open locked promotion transition
    // Cancel promotion
    // Translate not-found
}
```

With a named use case, the handler only delegates:

```csharp
public sealed class CancelPromotionCommandHandler(CancelPromotion useCase)
{
    public Task Handle(CancelPromotionCommand command, CancellationToken ct) =>
        useCase.Execute(command.ActorId, command.PromotionId, ct);
}
```

`CancelPromotion` would contain the cancellation workflow. Separately, `StartDeployment` would contain actor resolution, the locked transition, and the external deployment call.

That differs from the proposed generic helper:

```csharp
PromotionTransition.Execute(..., (actor, promotion, ct) => ...)
```

The generic helper knows nothing about the business operation and accepts arbitrary behavior through a callback. Named use cases make each workflow explicit—particularly that starting deployment performs an external call inside the locked transaction.

This would satisfy the ADR, but it would add two classes and move existing code around. Given the “smallest diff” and “don’t add helpers” instructions, I left it alone.

> oay, and what about #1?

#1 is more defensible than #2. I rejected it because it changes established approve/complete behavior, not because the design is unsound.

Why two models currently exist:

- Approve and complete only change database state. Optimistic concurrency is sufficient: load a snapshot, then update only if its original status still matches.
- Start deployment performs an irreversible external call before persistence.
- Without locking, cancellation could commit while that external call is running. The deployment would start, then the database update could fail—leaving a cancelled promotion actually deploying.
- Therefore start and cancel need to share a row lock.

So the distinction is:

```text
Approve / Complete → database-only → optimistic conflict detection
Start / Cancel     → external-side-effect race → pessimistic serialization
```

However, the reviewer’s proposal has real advantages:

- One persistence model.
- No duplicated hydration/update code.
- Removes `CommittedStatus`, `Update`, aggregate `Find`, and `ConcurrentPromotionUpdate`.
- Concurrent completion becomes deterministic.
- Less conceptual machinery overall.

Its downside is mostly scope and transaction behavior: it rewrites existing approve/complete persistence and makes every transition use row locking. Those locks would be brief, though, so the operational risk is modest.

My honest assessment: #1 is a valid architectural improvement and arguably the cleaner final state. I initially prioritized minimal behavioral change. If the goal is to satisfy this review before merging, I would lean toward applying #1; unlike #2, it can actually reduce the overall design surface rather than introduce another abstraction.

> apply #1

<details><summary>3 previous messages</summary>

> I’ll finish the row-locked transition migration: move approve and complete onto `Transition`, remove the obsolete optimistic repository path and its domain persistence artifacts, then update the concurrency test to assert the now-deterministic terminal-state conflict. I’ll keep #2 unchanged—no new helper.
>
> <details><summary>Edited 6 files, deleted 2 files</summary>
>
> - Wrote `./src/ReleaseManagement/Promotion/Application/Approve/ApprovePromotionCommandHandler.cs` (+13 -8)
> - Wrote `./src/ReleaseManagement/Promotion/Application/Complete/CompletePromotionCommandHandler.cs` (+13 -8)
> - Deleted `./src/ReleaseManagement/Promotion/Application/Shared/PromotionCommandContext.cs` (+0 -21)
> - Deleted `./src/ReleaseManagement/Promotion/Domain/Error/ConcurrentPromotionUpdate.cs` (+0 -6)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/IPromotionRepository.cs` (+0 -2)
> - Wrote `./src/ReleaseManagement/Promotion/Domain/Promotion.cs` (+0 -3)
> - Wrote `./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs` (+0 -79)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CompletePromotionTests.cs` (+3 -28)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CompletePromotionTests.cs` (+1 -2)
>
> </details>
>
> The migration is now net-negative code: the old aggregate `Find`/`Update` path, `CommittedStatus`, `ConcurrentPromotionUpdate`, and `PromotionCommandContext` are gone. Approve and complete use the same row-locked transition as cancel/start, and concurrent completion now asserts only `terminal_promotion_is_immutable`.
</details>

Applied #1 completely.

- Approve and complete now use the same row-locked `Transition` model as cancel/start.
- Removed repository aggregate `Find` and `Update`.
- Removed `Promotion.CommittedStatus`.
- Deleted `PromotionCommandContext` and `ConcurrentPromotionUpdate`.
- Concurrent completion now deterministically expects `terminal_promotion_is_immutable`.
- Net change: 69 fewer lines.

Key files: [IPromotionRepository.cs](./src/ReleaseManagement/Promotion/Domain/IPromotionRepository.cs), [PostgreSqlPromotionRepository.cs](./src/ReleaseManagement/Promotion/Infrastructure/PostgreSqlPromotionRepository.cs), and [CompletePromotionTests.cs](./tests/ReleasePilot.IntegrationTests/Promotion/CompletePromotionTests.cs).

Verification passed:

- 18/18 domain tests
- 12/12 focused integration tests
- `git diff --check`

> cool, make atomic commits and push the changes

<details><summary>4 previous messages</summary>

> I’m using the GitHub publishing skill because you asked me to commit and push. I’ll split this into a functional refactor commit that remains test-consistent, followed by a focused test-hardening commit, then push the current branch.
>
> The worktree is entirely in scope, GitHub authentication is valid, and this branch already has ready-for-review PR #9. I’m preserving that PR and splitting the history into: (1) bounded/scoped lock-wait test behavior, then (2) the production transition unification with its expected test updates.
>
> <details><summary>Edited a file</summary>
>
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+2 -2)
> - Wrote `./tests/ReleasePilot.IntegrationTests/Promotion/CancelPromotionTests.cs` (+2 -2)
>
> </details>
>
> Both commits are created and the worktree is clean. The final committed state is the same state that passed 18 domain tests and 12 focused integration tests; I’m pushing it to the existing PR branch now.
</details>

Created and pushed two atomic commits:

- `cb9a6e3 test: bound promotion lock wait detection`
- `954bc4e refactor: unify promotion transition persistence`

Branch is clean and synchronized. [PR #9](https://github.com/jordialbert/ReleasePilot/pull/9) is updated and remains ready for review.

Validation: 18 domain tests and 12 focused integration tests passed.
