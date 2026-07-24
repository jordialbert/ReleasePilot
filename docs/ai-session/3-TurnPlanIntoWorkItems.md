# Turn plan into local spec

> Turn the plan into a spec locally [$to-spec](~/.agents/skills/to-spec/SKILL.md)

<details><summary>5 previous messages</summary>

> I’m using the `to-spec` skill to synthesize the existing plan and repository context into a local spec. I’ll keep it local as requested, identify the highest practical test seam from the codebase, and won’t publish anything to the issue tracker.
>
> The repository is intentionally documentation-first: it contains the plan, project context, and seven ADRs, with no implementation yet. I’m reading those decision records now so the spec uses the project’s own release/promotion vocabulary and doesn’t contradict established architecture.
>
> The test seam is clear from the plan: the primary acceptance seam is one end-to-end journey through the public HTTP API, PostgreSQL, and worker; direct database assertions are limited to asynchronous effects with no public representation. Focused Domain and API integration tests remain supporting seams for invariant and concurrency risk. I’m carrying that decision into the local spec without pausing for an interview, as the skill’s synthesis-only rule requires.
>
> <details><summary>Created a file</summary>
>
> - Wrote `./SPEC.md` (+186 -0)
>
> </details>
>
> The local spec is drafted as a single new file. It preserves the ADR decisions, expands the plan into 72 user stories, and names one primary end-to-end acceptance seam plus only the focused supporting seams justified by domain and integration risk. I’m doing a final structural and diff check now.
</details>

Created the local specification: [SPEC.md](./SPEC.md).

It includes all required sections, 72 user stories, implementation decisions, test seams, scope boundaries, and ADR-aligned terminology. Nothing was published externally.

> now create tickets for it [$to-tickets](~/.agents/skills/to-tickets/SKILL.md) . For each ticket, I want you to discuss branch names and what changes will each branch include.
>
> I want branches named {feat|chore|deploy|docs|fix|refactor|test}/{small-branch-description}
>
> and I want atomic commits. I prefer commits and branches to be vertical slices rather than horizontal, but propose your approach and we will discuss

<details><summary>1 previous message</summary>

> I’m using `to-tickets` to turn the approved spec into tracer-bullet tickets. I’ll stop at the discussion checkpoint: propose the dependency graph, branch name, exact branch scope, and atomic commit plan for every ticket; I won’t create ticket files until you approve the breakdown.
</details>

I recommend 14 ticket branches, each landing as one atomic commit. Most are vertical feature slices; three deliberate exceptions are bootstrap, final acceptance verification, and documentation.

Tests belong in the same commit as the behavior they verify. No “implementation commit, then tests commit” split.

1. **Bootstrap a runnable ReleasePilot platform**
   - Branch: `chore/bootstrap-runnable-platform`
   - Blocked by: None
   - Delivers: A compiling .NET solution where the API, worker, and PostgreSQL can start together; the API exposes Swagger and basic health.
   - Branch changes: Project graph, composition roots, initial database initialization, Compose topology, API/worker startup, Swagger, and basic smoke verification.
   - Atomic commit: `chore: bootstrap runnable ReleasePilot platform`

2. **Request and inspect a Promotion**
   - Branch: `feat/request-promotion`
   - Blocked by: 1
   - Delivers: A user can request the first dev Promotion and retrieve its canonical details and immutable history.
   - Branch changes: Minimum typed Domain values, `Promotion.Request`, progression and active-target guards, `PromotionRequested`, persistence and transaction boundary, Application/version/user seeds, request/detail API paths, Problem Details, and focused Domain/API tests.
   - Atomic commit: `feat: request and inspect Promotions`

3. **Approve a Promotion**
   - Branch: `feat/approve-promotion`
   - Blocked by: 2
   - Delivers: An Approver can approve a Requested Promotion; a known non-Approver receives a safe `403`.
   - Branch changes: Approval transition and event, Approver authorization, actor resolution, approval endpoint, persisted history, error mapping, and behavior tests.
   - Atomic commit: `feat: approve Promotions as an Approver`

4. **Start an idempotent Deployment**
   - Branch: `feat/start-deployment`
   - Blocked by: 3
   - Delivers: An Approved Promotion can start Deployment safely, while upstream failure leaves it Approved.
   - Branch changes: Deployment transition and event, Deployment port and deterministic adapter, Promotion-ID idempotency, endpoint, `503 deployment_unavailable`, transaction ordering, and integration tests.
   - Atomic commit: `feat: start idempotent Deployments`

5. **Complete a Promotion and advance the Application Version**
   - Branch: `feat/complete-promotion`
   - Blocked by: 4
   - Delivers: A Deploying Promotion can complete, advance its Pipeline Position, expose deployed status, and permit only the next Environment.
   - Branch changes: Completion transition/event, completion endpoint, current deployed-version calculation, Application status query, dev-to-staging-to-production progression, terminal immutability, concurrent-update handling, and tests.
   - Atomic commit: `feat: complete Promotions and advance versions`

6. **Cancel a Promotion and retry its target**
   - Branch: `feat/cancel-promotion`
   - Blocked by: 3
   - Delivers: Requested or Approved Promotions can be cancelled, remain immutable, and be retried through a new Promotion.
   - Branch changes: Cancellation transition/event, endpoint, active-slot release, retry eligibility, canonical history, invalid-transition handling, and tests.
   - Atomic commit: `feat: cancel Promotions and retry targets`

7. **Roll back a Promotion and retry its target**
   - Branch: `feat/rollback-promotion`
   - Blocked by: 4
   - Delivers: A Deploying Promotion can be rolled back without changing the deployed version, then retried through a new Promotion.
   - Branch changes: Rollback transition/event, endpoint, retry eligibility, status-query behavior, invalid-transition handling, and tests.
   - Atomic commit: `feat: roll back Promotions and retry targets`

8. **Browse Application Promotion history**
   - Branch: `feat/list-promotion-history`
   - Blocked by: 2
   - Delivers: Users can browse a deterministic, bounded history of an Application’s Promotion attempts.
   - Branch changes: Paginated query/use case/response, total count, page-size validation and cap, stable newest-first ordering, endpoint, and integration tests.
   - Atomic commit: `feat: list Application Promotion history`

9. **Audit Domain Events asynchronously**
   - Branch: `feat/audit-promotion-events`
   - Blocked by: 2
   - Delivers: Every Domain Event is independently consumed into an idempotent Audit Log.
   - Branch changes: Consumer deliveries, transactional fan-out, worker claiming, leases, claim tokens, retries, terminal failure, Audit consumer, `TimeProvider`, structured processing logs, and queue/integration tests.
   - Atomic commit: `feat: audit Promotion events asynchronously`

10. **Notify terminal Promotion outcomes**
    - Branch: `feat/notify-terminal-promotions`
    - Blocked by: 5, 6, 7, 9
    - Delivers: Completion, cancellation, and rollback produce idempotent stakeholder notifications.
    - Branch changes: Terminal-event subscriptions, notification port and deterministic adapter, Event-ID idempotency, delivery acknowledgement/failure behavior, and tests for every terminal outcome.
    - Atomic commit: `feat: notify terminal Promotion outcomes`

11. **Generate Release Notes Drafts**
    - Branch: `feat/generate-release-notes`
    - Blocked by: 3, 9
    - Delivers: Approval triggers a deterministic tool-calling agent whose submitted Markdown draft appears in Promotion details.
    - Branch changes: Release Notes persistence, Work Item and language-model ports/adapters, agent loop, four typed tools, Breaking Change state, ten-turn limit, Approved-event consumer, nullable detail response, retries, and agent/integration tests.
    - Atomic commit: `feat: generate Release Notes Drafts`

12. **Verify the complete release journey**
    - Branch: `chore/verify-release-journey`
    - Blocked by: 8, 10, 11
    - Delivers: One executable acceptance journey proves dev → staging → production through HTTP, PostgreSQL, and the worker.
    - Branch changes: Isolated PostgreSQL test setup and the single highest-seam journey asserting public history/status/draft behavior plus otherwise-private Audit Log and delivery effects.
    - Atomic commit: `chore: verify the complete release journey`

13. **Package ReleasePilot for local operation**
    - Branch: `deploy/package-release-pilot`
    - Blocked by: 12
    - Delivers: The finished platform starts reliably with one Docker Compose command and shuts down safely.
    - Branch changes: Cache-efficient multi-stage image, selectable executable, non-root runtime, Compose health dependencies and restart policy, finalized schema health check, worker shutdown, polling defaults, structured runtime logging, and container smoke verification.
    - Atomic commit: `deploy: package ReleasePilot for local operation`

14. **Complete the evaluator handoff**
    - Branch: `docs/complete-project-handoff`
    - Blocked by: 13
    - Delivers: An evaluator can start, exercise, reset, understand, and present ReleasePilot without reading the implementation.
    - Branch changes: README, seed identifiers, copy-paste API examples, test/reset instructions, architecture and trade-offs, event/agent explanations, limitations, walkthrough, and AI-session handoff instructions.
    - Atomic commit: `docs: document the ReleasePilot handoff`

The dependency shape allows useful parallel work: 6 can proceed alongside 4; 8 and 9 can start after 2; and 11 can proceed once approval and event delivery exist.

The main merge/split decision is tickets 6 and 7. I recommend keeping them separate because cancellation and rollback have different lifecycle rules and can be implemented independently. Does this granularity feel right, are the blocking edges accurate, and would you merge or split any tickets before I create the local ticket files?

> I want all tickets to include some more commits. Some tickets looks like they will include a bunch of work in a single commit. I want work to be splitted into more commits.
>
> ticket 12 is mainly focused on tests, so branch should be test/...
>
> why is ticket 13 needed if ticket 1 is for bootrsaping the project? why not create the compose and dockerfile already as they need to be, instead of having to update them later?

Agreed on both points.

Ticket 13 was unnecessary duplication. I originally separated initial bootstrapping from operational hardening, but in a greenfield project the Dockerfile and Compose topology can be correct from the start. I’ve folded the production-shaped containers, non-root runtime, health dependencies, restart policy, logging, and shutdown setup into ticket 1.

I also revised every branch to contain several atomic, green commits. Each commit adds a coherent behavior or independently verifiable capability; tests accompany the behavior they cover. These commits should be preserved when merging rather than squash-merged.

## Revised breakdown

1. **Bootstrap a runnable ReleasePilot platform**
   - Branch: `chore/bootstrap-runnable-platform`
   - Blocked by: None
   - Delivers: The final API, worker, and PostgreSQL topology starts through Docker Compose.
   - Scope: Solution structure, application hosts, foundational schema/seeds, final Dockerfile, final Compose topology, Swagger, health, structured logging, graceful shutdown, and smoke checks.
   - Commits:
     1. `chore: scaffold ReleasePilot solution and application hosts`
     2. `chore: initialize PostgreSQL schema and seed data`
     3. `deploy: build production API and worker images`
     4. `deploy: run ReleasePilot with Docker Compose`

   The Dockerfile and Compose definitions should not need later structural rework. Feature branches may extend the schema and registrations as their vertical behavior requires.

2. **Request and inspect a Promotion**
   - Branch: `feat/request-promotion`
   - Blocked by: 1
   - Delivers: A known user can request a dev Promotion and inspect its canonical details and history.
   - Scope: Promotion creation, typed values, initial persistence, transactional `PromotionRequested` event, request/detail endpoints, public errors, and tests.
   - Commits:
     1. `feat: request and inspect dev Promotions`
     2. `fix: reject invalid initial Promotion targets`
     3. `fix: prevent competing Active Promotions`

3. **Approve a Promotion**
   - Branch: `feat/approve-promotion`
   - Blocked by: 2
   - Delivers: Approvers can authorize Requested Promotions, including their own requests.
   - Scope: Approval transition/event, actor resolution, role authorization, endpoint, history, errors, and tests.
   - Commits:
     1. `feat: approve Requested Promotions`
     2. `fix: restrict Promotion approval to Approvers`
     3. `fix: reject invalid Promotion approvals`

4. **Start an idempotent Deployment**
   - Branch: `feat/start-deployment`
   - Blocked by: 3
   - Delivers: An Approved Promotion starts Deployment safely.
   - Scope: Deployment transition/event, external port, deterministic adapter, idempotency, endpoint, transaction ordering, upstream errors, and tests.
   - Commits:
     1. `feat: start Deployment for Approved Promotions`
     2. `feat: deduplicate Deployment requests by Promotion`
     3. `fix: preserve Approved state when Deployment fails`

5. **Complete a Promotion and advance the version**
   - Branch: `feat/complete-promotion`
   - Blocked by: 4
   - Delivers: Successful Deployment advances the Application Version through the Environment Pipeline.
   - Scope: Completion, deployed-version status, next-Environment progression, completed-target guards, production finality, concurrency handling, and tests.
   - Commits:
     1. `feat: complete Deploying Promotions`
     2. `feat: show deployed Application versions by Environment`
     3. `feat: advance Application Versions to the next Environment`
     4. `fix: prevent Promotion beyond completed production`

6. **Cancel a Promotion and retry its target**
   - Branch: `feat/cancel-promotion`
   - Blocked by: 3
   - Delivers: Requested or Approved Promotions can be cancelled and retried with a new identity.
   - Scope: Cancellation states/event, endpoint, active-slot release, retry behavior, immutability, and tests.
   - Commits:
     1. `feat: cancel Requested Promotions`
     2. `feat: cancel Approved Promotions`
     3. `feat: retry cancelled Promotion targets`

7. **Roll back a Promotion and retry its target**
   - Branch: `feat/rollback-promotion`
   - Blocked by: 4
   - Delivers: A Deploying Promotion can be rolled back without replacing the deployed version.
   - Scope: Rollback transition/event, endpoint, deployed-status behavior, retry behavior, immutability, and tests.
   - Commits:
     1. `feat: roll back Deploying Promotions`
     2. `fix: preserve deployed versions after rollback`
     3. `feat: retry rolled-back Promotion targets`

8. **Browse Application Promotion history**
   - Branch: `feat/list-promotion-history`
   - Blocked by: 2
   - Delivers: Users can browse stable, bounded Promotion history.
   - Scope: Query path, pagination, count, validation, stable ordering, endpoint, and integration tests.
   - Commits:
     1. `feat: list the first page of Promotion history`
     2. `feat: navigate Promotion history pages`
     3. `fix: bound and deterministically order Promotion history`

9. **Audit Domain Events asynchronously**
   - Branch: `feat/audit-promotion-events`
   - Blocked by: 2
   - Delivers: Domain Events are consumed into an independent Audit Log through PostgreSQL-backed delivery.
   - Scope: Delivery fan-out, worker claims, Audit consumer, leases, claim tokens, retries, terminal failure, idempotency, time control, and tests.
   - Commits:
     1. `feat: audit Promotion events asynchronously`
     2. `fix: protect event delivery claims with leases`
     3. `feat: retry failed event deliveries`
     4. `fix: make Audit Log consumption idempotent`

10. **Notify terminal Promotion outcomes**
    - Branch: `feat/notify-terminal-promotions`
    - Blocked by: 5, 6, 7, 9
    - Delivers: Stakeholders receive idempotent notifications for every terminal outcome.
    - Scope: Terminal subscriptions, notification port/adapter, delivery handling, Event-ID idempotency, and tests.
    - Commits:
      1. `feat: notify completed Promotions`
      2. `feat: notify cancelled Promotions`
      3. `feat: notify rolled-back Promotions`
      4. `fix: deduplicate terminal notifications by event`

11. **Generate Release Notes Drafts**
    - Branch: `feat/generate-release-notes`
    - Blocked by: 3, 9
    - Delivers: Approval triggers a deterministic agent that produces a visible Release Notes Draft.
    - Scope: Draft persistence, model and Work Item ports, agent loop, typed tools, Breaking Changes, validation, limits, consumer delivery, detail response, and tests.
    - Commits:
      1. `feat: generate Release Notes Drafts from Work Items`
      2. `feat: let the agent clarify ambiguous Work Items`
      3. `feat: record Breaking Changes in Release Notes Drafts`
      4. `fix: reject invalid or incomplete agent runs`
      5. `fix: bound Release Notes generation to ten model turns`

12. **Verify the complete release journey**
    - Branch: `test/verify-release-journey`
    - Blocked by: 8, 10, 11
    - Delivers: The highest-seam acceptance journey proves the entire system.
    - Scope: Isolated PostgreSQL setup, full dev → staging → production journey, public API assertions, and limited database assertions for private asynchronous effects.
    - Commits:
      1. `chore: configure isolated end-to-end test databases`
      2. `chore: verify the complete Environment Pipeline journey`
      3. `chore: verify asynchronous release effects`

    The branch uses `test/` as requested. The commits use `chore:` because the repository’s commit-message convention does not define a `test:` commit type.

13. **Complete the evaluator handoff**
    - Branch: `docs/complete-project-handoff`
    - Blocked by: 12
    - Delivers: An evaluator can run, exercise, understand, and present ReleasePilot.
    - Scope: Startup/reset instructions, seed data, API examples, architecture, event processing, AI agent, limitations, walkthrough, and AI-session handoff.
    - Commits:
      1. `docs: explain local startup and database reset`
      2. `docs: add Promotion command and query examples`
      3. `docs: explain architecture and operational trade-offs`
      4. `docs: add walkthrough and AI session handoff`

This gives us 45 commits across 13 branches. The commits are small enough to review independently while avoiding layer-only commits such as “add repositories” or “add controllers.”

If this revision looks right, I’ll create the 13 local ticket files with these branch and commit plans included.

> fix commits should only be for hotfix or bugfix, so most of your "fix" commits are really "feat" commits

Correct. Those are planned capabilities and invariants, not bug fixes. They should use `feat:`. I’ll reserve `fix:` for an actual defect discovered after behavior has been implemented.

The corrected commits are:

- Ticket 2:
  - `feat: reject invalid initial Promotion targets`
  - `feat: prevent competing Active Promotions`

- Ticket 3:
  - `feat: restrict Promotion approval to Approvers`
  - `feat: reject invalid Promotion approvals`

- Ticket 4:
  - `feat: preserve Approved state when Deployment fails`

- Ticket 5:
  - `feat: prevent Promotion beyond completed production`

- Ticket 7:
  - `feat: preserve deployed versions after rollback`

- Ticket 8:
  - `feat: bound and deterministically order Promotion history`

- Ticket 9:
  - `feat: protect event delivery claims with leases`
  - `feat: make Audit Log consumption idempotent`

- Ticket 10:
  - `feat: deduplicate terminal notifications by event`

- Ticket 11:
  - `feat: reject invalid or incomplete agent runs`
  - `feat: bound Release Notes generation to ten model turns`

The commit-type policy will therefore be:

- `feat:` planned product behavior, invariants, error handling, and resilience.
- `chore:` scaffolding and test infrastructure.
- `deploy:` containerization and runtime packaging.
- `docs:` documentation.
- `fix:` only an actual bug fix or hotfix.
- `refactor:` behavior-preserving restructuring.

Everything else in the 13-ticket breakdown remains unchanged. Once approved, I’ll create the local ticket files with the corrected commit plans.

> for test commits, it should start with "test:". Send me again the full list of tickets, branch names, what will be done in each ticket and commits proposed

One constraint to flag: the workspace commit convention only permits `feat`, `fix`, `chore`, `deploy`, `docs`, and `refactor`. It does not permit `test:`. I’ll use the requested `test/` branch prefix, but test-only commits must use `chore:` unless that higher-level convention changes.

## 1. Bootstrap a runnable ReleasePilot platform

- Branch: `chore/bootstrap-runnable-platform`
- Blocked by: None
- What will be done: Create the .NET solution, API and worker hosts, project dependencies, PostgreSQL foundation, final multi-stage Dockerfile, final Compose topology, Swagger, health checks, structured logging, non-root containers, and graceful shutdown.
- Proposed commits:
  1. `chore: scaffold ReleasePilot solution and application hosts`
  2. `chore: initialize PostgreSQL schema and seed data`
  3. `deploy: build production API and worker images`
  4. `deploy: run ReleasePilot with Docker Compose`

## 2. Request and inspect a Promotion

- Branch: `feat/request-promotion`
- Blocked by: 1
- What will be done: Add typed Domain values, request creation, initial Environment validation, active-target uniqueness, transactional event persistence, canonical Promotion details and history, HTTP errors, and Domain/API tests.
- Proposed commits:
  1. `feat: request and inspect dev Promotions`
  2. `feat: reject invalid initial Promotion targets`
  3. `feat: prevent competing Active Promotions`

## 3. Approve a Promotion

- Branch: `feat/approve-promotion`
- Blocked by: 2
- What will be done: Add approval transition and event, acting-user resolution, Approver authorization, self-approval, approval endpoint, history updates, controlled transition errors, and tests.
- Proposed commits:
  1. `feat: approve Requested Promotions`
  2. `feat: restrict Promotion approval to Approvers`
  3. `feat: reject invalid Promotion approvals`

## 4. Start an idempotent Deployment

- Branch: `feat/start-deployment`
- Blocked by: 3
- What will be done: Add the Deployment transition and event, external Deployment port, deterministic adapter, Promotion-ID idempotency, endpoint, transaction ordering, safe upstream failure handling, and integration tests.
- Proposed commits:
  1. `feat: start Deployment for Approved Promotions`
  2. `feat: deduplicate Deployment requests by Promotion`
  3. `feat: preserve Approved state when Deployment fails`

## 5. Complete a Promotion and advance the version

- Branch: `feat/complete-promotion`
- Blocked by: 4
- What will be done: Add completion, Pipeline Position advancement, deployed-version calculation, Application status query, next-Environment progression, completed-target protection, production finality, concurrency handling, and tests.
- Proposed commits:
  1. `feat: complete Deploying Promotions`
  2. `feat: show deployed Application versions by Environment`
  3. `feat: advance Application Versions to the next Environment`
  4. `feat: prevent Promotion beyond completed production`

## 6. Cancel a Promotion and retry its target

- Branch: `feat/cancel-promotion`
- Blocked by: 3
- What will be done: Add cancellation from Requested and Approved, the cancellation event, endpoint, terminal immutability, active-slot release, retry through a new Promotion, and tests.
- Proposed commits:
  1. `feat: cancel Requested Promotions`
  2. `feat: cancel Approved Promotions`
  3. `feat: retry cancelled Promotion targets`

## 7. Roll back a Promotion and retry its target

- Branch: `feat/rollback-promotion`
- Blocked by: 4
- What will be done: Add rollback from Deploying, the rollback event, endpoint, deployed-version preservation, terminal immutability, retry through a new Promotion, and tests.
- Proposed commits:
  1. `feat: roll back Deploying Promotions`
  2. `feat: preserve deployed versions after rollback`
  3. `feat: retry rolled-back Promotion targets`

## 8. Browse Application Promotion history

- Branch: `feat/list-promotion-history`
- Blocked by: 2
- What will be done: Add Application Promotion history, total count, page-number navigation, page-size validation and limits, deterministic newest-first ordering, endpoint, and integration tests.
- Proposed commits:
  1. `feat: list the first page of Promotion history`
  2. `feat: navigate Promotion history pages`
  3. `feat: bound and deterministically order Promotion history`

## 9. Audit Domain Events asynchronously

- Branch: `feat/audit-promotion-events`
- Blocked by: 2
- What will be done: Add consumer deliveries, transactional fan-out, worker claims, Audit Log consumption, leases, claim tokens, retries, terminal failure, idempotency, controlled time, structured logs, and queue tests.
- Proposed commits:
  1. `feat: audit Promotion events asynchronously`
  2. `feat: protect event delivery claims with leases`
  3. `feat: retry failed event deliveries`
  4. `feat: make Audit Log consumption idempotent`

## 10. Notify terminal Promotion outcomes

- Branch: `feat/notify-terminal-promotions`
- Blocked by: 5, 6, 7, 9
- What will be done: Subscribe to completion, cancellation, and rollback events; add the notification port and deterministic adapter; use Event IDs for idempotency; and verify delivery outcomes.
- Proposed commits:
  1. `feat: notify completed Promotions`
  2. `feat: notify cancelled Promotions`
  3. `feat: notify rolled-back Promotions`
  4. `feat: deduplicate terminal notifications by event`

## 11. Generate Release Notes Drafts

- Branch: `feat/generate-release-notes`
- Blocked by: 3, 9
- What will be done: Add Release Notes persistence, model and Work Item ports, deterministic adapters, tool-calling agent loop, clarification, Breaking Changes, validation, model-turn limits, Approved-event consumption, detail response, and tests.
- Proposed commits:
  1. `feat: generate Release Notes Drafts from Work Items`
  2. `feat: let the agent clarify ambiguous Work Items`
  3. `feat: record Breaking Changes in Release Notes Drafts`
  4. `feat: reject invalid or incomplete agent runs`
  5. `feat: bound Release Notes generation to ten model turns`

## 12. Verify the complete release journey

- Branch: `test/verify-release-journey`
- Blocked by: 8, 10, 11
- What will be done: Configure isolated PostgreSQL test databases and verify the complete dev → staging → production journey through HTTP, PostgreSQL, and the worker. Public behavior will be asserted through the API; direct database assertions will cover only private asynchronous effects.
- Proposed commits under the configured convention:
  1. `chore: configure isolated end-to-end test databases`
  2. `chore: verify the complete Environment Pipeline journey`
  3. `chore: verify asynchronous release effects`

## 13. Complete the evaluator handoff

- Branch: `docs/complete-project-handoff`
- Blocked by: 12
- What will be done: Document startup, reset, seeds, every command and query, example workflows, architecture, event processing, Release Notes agent, trade-offs, limitations, walkthrough, and AI-session handoff.
- Proposed commits:
  1. `docs: explain local startup and database reset`
  2. `docs: add Promotion command and query examples`
  3. `docs: explain architecture and operational trade-offs`
  4. `docs: add walkthrough and AI session handoff`

This version has 13 branches and 45 atomic commits. `fix:` is reserved for genuine defects discovered during implementation.

> Proposed commits under the configured convention:chore: configure isolated end-to-end test databases
> chore: verify the complete Environment Pipeline journey
> chore: verify asynchronous release effects
>
> these commits should be "test: ..."
>
> other than that, you can create the tickets

<details><summary>3 previous messages</summary>

> The ticket breakdown is approved, so the `to-tickets` skill is moving from discussion to local publication. I’ll create one dependency-ordered file per ticket under `.scratch/release-pilot/issues`, including the branch scope, acceptance criteria, and atomic commit plan; ticket 12 will record the requested `test:` commits.
>
> <details><summary>Created 13 files</summary>
>
> - Wrote `./.scratch/release-pilot/issues/01-bootstrap-runnable-platform.md` (+24 -0)
> - Wrote `./.scratch/release-pilot/issues/02-request-promotion.md` (+24 -0)
> - Wrote `./.scratch/release-pilot/issues/03-approve-promotion.md` (+24 -0)
> - Wrote `./.scratch/release-pilot/issues/04-start-deployment.md` (+24 -0)
> - Wrote `./.scratch/release-pilot/issues/05-complete-promotion.md` (+26 -0)
> - Wrote `./.scratch/release-pilot/issues/06-cancel-promotion.md` (+24 -0)
> - Wrote `./.scratch/release-pilot/issues/07-rollback-promotion.md` (+24 -0)
> - Wrote `./.scratch/release-pilot/issues/08-list-promotion-history.md` (+24 -0)
> - Wrote `./.scratch/release-pilot/issues/09-audit-promotion-events.md` (+27 -0)
> - Wrote `./.scratch/release-pilot/issues/10-notify-terminal-promotions.md` (+25 -0)
> - Wrote `./.scratch/release-pilot/issues/11-generate-release-notes.md` (+28 -0)
> - Wrote `./.scratch/release-pilot/issues/12-verify-release-journey.md` (+24 -0)
> - Wrote `./.scratch/release-pilot/issues/13-complete-project-handoff.md` (+24 -0)
>
> </details>
>
> All 13 ticket files now exist with dependency edges, `ready-for-agent` status, branch scopes, acceptance criteria, and multi-commit plans. I’m checking numbering, branch-name conformity, and that ticket 12 contains exactly the requested `test:` commits.
</details>

Created 13 local tickets under [`.scratch/release-pilot/issues`](./.scratch/release-pilot/issues).

Each includes:

- Dependency edges and `ready-for-agent` status
- Approved branch name
- Detailed branch scope
- Atomic commit plan
- Acceptance criteria

Ticket 12 uses `test/verify-release-journey` and all three proposed commits use the `test:` prefix.
