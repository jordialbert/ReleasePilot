# ReleasePilot Implementation Plan

## Goal

Build the ReleasePilot challenge as a working internal platform that advances Application Versions through `dev → staging → production`. The implementation prioritizes the Promotion aggregate, explicit CQRS, transactional Domain Events, PostgreSQL-backed asynchronous processing, and a deterministic AI release-notes agent.

## Scope

The committed scope includes:

- All six Promotion commands and three required queries.
- Domain-enforced lifecycle, authorization, progression, uniqueness, and immutability.
- PostgreSQL 18 persistence and event delivery.
- Audit, terminal-notification, and release-notes consumers hosted in one worker process.
- In-memory adapters for every external system.
- A real tool-calling agent loop backed by a mocked language model.
- Docker Compose startup, Swagger UI, health checks, focused tests, README instructions, and walkthrough material.

The following are deliberately out of scope:

- Real authentication or external integrations.
- Catalog-management APIs.
- Configurable environment pipelines.
- Redeploying a successfully completed version to the same Environment.
- Database migrations.
- A dedicated message broker.
- Queue administration or retention jobs.
- A frontend or production observability platform.

GitHub Actions CI is a nice-to-have only if time remains.

## Architecture

Release Management is the only bounded context. ReleasePilot is the product and executable grouping.

```text
/
├── apps/
│   └── ReleasePilot/
│       ├── Api/
│       └── Worker/
├── src/
│   └── ReleaseManagement/
│       ├── Domain/
│       ├── Application/
│       └── Infrastructure/
├── tests/
│   ├── ReleaseManagement.Domain.Tests/
│   ├── ReleasePilot.IntegrationTests/
│   └── ReleasePilot.EndToEndTests/
├── database/
│   ├── 001-schema.sql
│   └── 002-seed.sql
├── docs/
│   ├── adr/
│   ├── ai-session/
│   └── walkthrough.md
├── CONTEXT.md
├── Dockerfile
├── docker-compose.yml
└── ReleasePilot.sln
```

Projects and dependencies:

```text
ReleaseManagement.Domain          → no project dependency
ReleaseManagement.Application     → Domain
ReleaseManagement.Infrastructure  → Application + Domain
API and worker                    → Application + Infrastructure
```

No `src/Shared` directory will be created unless a genuinely cross-context concept emerges.

## CQRS and use cases

Commands and queries use the same Application structure:

```text
Controller
  → Command or Query Handler
    → named Application use case
      → Domain repositories and Domain or Application ports
    ← Application response, for queries
  ← HTTP response
```

Handlers are thin CQRS entry points. Use cases own orchestration. Query use cases map Domain objects into consumer-shaped Application responses. Controllers never serialize Domain aggregates.

There is no MediatR dependency, custom command bus, custom query bus, or generic handler abstraction. Controllers inject concrete handlers.

Example folders:

```text
Application/Promotions/Request/
├── RequestPromotionCommand.cs
├── RequestPromotionCommandHandler.cs
└── PromotionRequester.cs

Application/Promotions/GetDetails/
├── GetPromotionDetailsQuery.cs
├── GetPromotionDetailsQueryHandler.cs
├── PromotionDetailsFinder.cs
└── PromotionDetailsResponse.cs
```

## Domain model

### Promotion lifecycle

```text
Requested ──approve──> Approved ──start──> Deploying ──complete──> Completed
    │                     │                    │
    └────cancel───────────┘                    └──rollback──> RolledBack
```

`Completed`, `Cancelled`, and `RolledBack` are terminal and immutable.

- Cancellation is valid from Requested or Approved.
- Rollback is valid only while Deploying.
- A cancelled or rolled-back attempt can be retried only by creating a new Promotion targeting the same Environment.
- A successful completion advances the version’s Pipeline Position.
- A successfully completed Environment cannot be targeted again by that version.
- Completing production ends that version’s progression.

### Creation invariants

The first Promotion targets dev. Later Promotions target exactly the Environment after the version’s last successfully completed Environment.

Only one Active Promotion may exist for an `(Application, target Environment)` pair across all versions of that Application. Requested, Approved, and Deploying are active.

The Promotion factory receives:

- The version’s last successfully completed Environment.
- Whether the Application already has an Active Promotion for the target.

The use case loads these contextual facts, but `Promotion.Request` decides whether they are valid and throws the Domain error. A PostgreSQL partial unique index closes the concurrent-request race.

### Authorization

Seeded users have either the Operator or Approver role. Any known seeded user may request, start, complete, roll back, or cancel. Only an Approver may approve. An Approver may approve their own request.

### Typed values

The Domain defines typed IDs and business values:

```text
PromotionId
ApplicationId
ApplicationVersionId
UserId
DomainEventId
ApplicationVersionLabel
Actor
DeploymentEnvironment
PromotionStatus
UserRole
```

IDs use small `readonly record struct` wrappers around `Guid`. IDs are UUIDv7 values created by the application. `ApplicationVersionLabel` is a non-empty opaque string and is not parsed as semantic versioning.

`DeploymentEnvironment`, `PromotionStatus`, and `UserRole` are typed in C# Domain code and persisted as checked PostgreSQL text.

### Domain errors

`DomainException` is the base class for specific Domain errors:

```text
ActivePromotionAlreadyExists
EnvironmentSkipped
EnvironmentAlreadyCompleted
InvalidPromotionTransition
OnlyApproverCanApprove
TerminalPromotionIsImmutable
```

Each exposes a stable code for Problem Details. Missing resources and malformed HTTP input are not Domain exceptions.

### Domain Events

Each successful transition records exactly one typed event:

```text
PromotionRequested
PromotionApproved
DeploymentStarted
PromotionCompleted
PromotionRolledBack
PromotionCancelled
```

Promotion exposes no public status setter.

## Domain and Application ports

Repository and Promotion-facing external-system interfaces live in Domain. Use-case orchestration interfaces live in Application. Adapters live in Infrastructure.

Domain ports include:

```text
IApplicationRepository
IApplicationVersionRepository
IPromotionRepository
IUserRepository
IDomainEventRepository
IReleaseNotesRepository
IAuditLogRepository
IDomainEventBus
IDeploymentPort
IIssueTrackerPort
INotificationPort
```

Application ports include:

```text
IUnitOfWork
ILanguageModelPort
```

No generic repository is introduced.

The issue-tracker port owns both linked-work-item lookup and clarification:

```text
GetWorkItems(promotionId)
AskClarification(workItemId, question)
```

All external adapters are deterministic and in-memory:

- Deployment logs requests and deduplicates by Promotion ID.
- Issue Tracker returns fixed linked Work Items and clarifications.
- Notification logs terminal-state alerts and receives the Event ID as its idempotency key.
- Language Model returns deterministic typed tool calls.

## Transactions and external effects

A command use case stages the aggregate snapshot and its events before committing:

```text
Repository.Save
→ DomainEventBus.Publish(Promotion.PullDomainEvents())
→ UnitOfWork.Commit
```

The EF repository, PostgreSQL event bus, and Unit of Work share a scoped `DbContext`. One `SaveChangesAsync` transaction persists the Promotion, immutable Domain Events, and all consumer deliveries.

Named PostgreSQL constraint violations are translated into the appropriate Domain conflict. EF/Npgsql `xmin` conflicts become controlled concurrency conflicts.

Starting deployment follows:

```text
Load Promotion
→ apply StartDeployment in memory
→ invoke DeploymentPort using Promotion ID as idempotency key
→ stage Promotion and event
→ commit
```

If the port fails, no commit occurs and the stored Promotion remains Approved. The API logs the original error and returns a safe `503 deployment_unavailable` Problem Detail. A timeout after an external success is safe to retry because the idempotency key is stable.

## Database

PostgreSQL 18 stores:

```text
users
applications
application_versions
promotions
domain_events
event_deliveries
audit_log
release_notes_drafts
```

There is no Environments table, current-deployments table, event-sourcing table, separate Promotion-history table, Work Items table, or agent-run table.

### Promotion snapshot

`promotions` contains:

```text
id
application_id
application_version_id
target_environment
status
requested_by
requested_at
completed_at
```

PostgreSQL’s implicit `xmin` is mapped as an EF shadow concurrency token. No explicit concurrency column or Domain property is added.

Important constraints and indexes:

- Composite foreign key from `(application_id, application_version_id)` to the corresponding Application Version.
- Partial unique index on `(application_id, target_environment)` for active statuses.
- Partial completed-Promotion index supporting current deployed-version lookup.
- Unique version label per Application.
- Text checks for valid persisted statuses, roles, and environments.

The current deployed version in each Environment is the latest successfully completed Promotion for that Application and Environment. Cancellation and rollback do not replace it.

### Domain Events

`domain_events` is append-only:

```text
id
sequence
promotion_id
type
occurred_at
actor_id
payload
```

`sequence` provides deterministic history ordering. Common routing and audit metadata uses columns; event-specific facts use JSONB. Promotion detail history reads this table directly.

### Event deliveries

`event_deliveries` is the mutable PostgreSQL queue:

```text
event_id             composite primary key
consumer             composite primary key
status
attempts
available_at
locked_until
lock_token
finished_at
last_error
```

Statuses are `pending`, `completed`, and `failed`. A pending row with an active lease is being processed.

Consumers claim with `FOR UPDATE SKIP LOCKED`. Each claim:

- Increments attempts before invoking the handler.
- Assigns a new claim token.
- Extends the lease.

Acknowledgement and failure updates require the current claim token, preventing a stale worker from completing a reclaimed delivery.

Retry delays are 5, 10, 20, and 30 seconds. The fifth failed attempt marks the delivery Failed. Completed and failed deliveries are retained; manual replay is an SQL operation for this challenge.

Delivery fan-out:

```text
Audit consumer          ← every event
Notification consumer   ← Completed, Cancelled, RolledBack
Release Notes consumer  ← Approved
```

Database consumer effects and delivery completion commit together where possible. External notifications use at-least-once delivery and the Event ID as an idempotency key.

### Audit Log

The Audit consumer independently persists:

```text
event_id
type
promotion_id
occurred_at
actor_id
recorded_at
```

`event_id` is unique. The duplicated event fields intentionally demonstrate asynchronous consumption as required by the challenge.

### Release Notes

`release_notes_drafts` contains:

```text
id
promotion_id
triggering_event_id
content
breaking_changes
created_at
```

Promotion ID and triggering Event ID are unique. `content` is the full Markdown draft. `breaking_changes` is a JSONB array of:

```json
{
  "workItemId": "APP-142",
  "reason": "Removes the deprecated v1 authentication endpoint."
}
```

An empty list is stored as `[]`.

### Schema initialization

`database/001-schema.sql` creates the full schema. `database/002-seed.sql` inserts fixed Applications, Application Versions, and users. No migrations or runtime schema creation are used.

## HTTP API

### Commands

```http
POST /promotions
POST /promotions/{id}/approve
POST /promotions/{id}/start-deployment
POST /promotions/{id}/complete
POST /promotions/{id}/rollback
POST /promotions/{id}/cancel
```

All commands require `X-User-Id`.

Creating a Promotion returns `201 Created`, the full Promotion detail representation, and:

```http
Location: /promotions/{id}
```

After the command commits, the controller invokes the existing detail Query Handler to obtain the canonical representation. Other successful transitions return `204 No Content`.

### Queries

```http
GET /promotions/{id}
GET /applications/{id}/status
GET /applications/{id}/promotions?page=1&pageSize=20
```

Promotion detail includes immutable state history and a nullable asynchronous Release Notes Draft.

Application status always returns dev, staging, and production. Each Environment distinguishes the currently deployed version from any Active Promotion targeting it.

Promotion history uses page-number pagination ordered by `requested_at DESC, id DESC`, reports total count, and caps page size at 100.

### Errors

Responses use Problem Details plus stable `code` and `traceId` fields:

```text
400  malformed input or unknown value
401  missing or unknown acting user
403  known Operator attempts approval
404  missing resource
409  Domain invariant or concurrency conflict
503  unavailable external deployment system
500  unexpected failure
```

Raw database and upstream errors are never returned. Public idempotency-key storage is out of scope; repeated commands produce controlled conflicts.

Swagger UI is enabled by the API in every environment and is available at `/swagger`.

## AI release-notes agent

The Release Notes consumer consumes `PromotionApproved` and runs an Application-owned tool loop:

```text
Language Model response
→ validate typed tool calls
→ execute tools
→ append results to the conversation
→ invoke the model again
→ stop only after SubmitReleaseNotes succeeds
```

Tools:

```text
GetWorkItems          → IssueTrackerPort
AskClarification      → IssueTrackerPort
FlagBreakingChange    → current run’s structured state
SubmitReleaseNotes    → ReleaseNotesRepository
```

The loop allows at most ten model turns. Unknown tools, invalid arguments, or completion without submission fail processing and invoke the normal event-delivery retry policy.

Tool calls are logged structurally but not persisted in separate tables. The mocked LLM is deterministic; the loop remains generic and does not branch on the concrete adapter.

## Docker

One cache-efficient multi-stage Dockerfile builds either executable through an `APP_PROJECT` build argument.

Build order:

1. Copy the solution, project files, and any shared build/package property files.
2. Restore the selected project with `mcr.microsoft.com/dotnet/sdk:10.0.302-noble`.
3. Copy remaining source.
4. Publish with `--no-restore`.
5. Copy published output into `mcr.microsoft.com/dotnet/aspnet:10.0.10-noble`.
6. Run as the official non-root application user.

`.dockerignore` excludes Git metadata, `bin`, `obj`, and test results.

Compose services:

```text
db
api
worker
```

All services use `restart: unless-stopped`. Both application services depend on the healthy, initialized `db` service. The worker hosts the Audit, Notification, and Release Notes consumers; each consumer remains independently extractable into its own executable later.

PostgreSQL uses `postgres:18`, mounts initialization scripts read-only at `/docker-entrypoint-initdb.d`, and mounts its named data volume at PostgreSQL 18’s `/var/lib/postgresql` location.

The API exposes:

```text
http://localhost:8080
http://localhost:8080/swagger
http://localhost:8080/health
```

Demo queue defaults:

```text
Poll interval       500 ms
Lease duration      60 seconds
Maximum attempts    5
Maximum retry delay 30 seconds
```

Changing schema or seeds requires an explicit demo reset:

```bash
docker compose down --volumes
docker compose up --build
```

## Logging and health

Use built-in `ILogger` JSON console logging without Serilog. Relevant structured fields include trace ID, Promotion ID, Event ID, consumer, attempt, and status.

`/health` checks PostgreSQL connectivity and the initialized schema. The worker stops claiming on cancellation, finishes current work within the shutdown timeout when possible, and otherwise relies on lease expiry for recovery.

Use the built-in `TimeProvider` for event timestamps, retry timing, and time-sensitive tests.

## Tests

Tests exist only where they demonstrate business behavior or integration risk.

### Domain unit tests

Unit-test key Promotion behavior:

- Valid lifecycle.
- Invalid transitions.
- Pipeline progression and skipped Environments.
- Active target conflict.
- approval authorization.
- terminal-state immutability.
- cancellation and rollback retry eligibility.
- emitted Domain Events.

Do not test trivial getters, DTOs, or constructors without behavior.

### API integration tests

Integration tests use `WebApplicationFactory` and real PostgreSQL 18 through Testcontainers. They call API endpoints only and assert public HTTP responses.

Each test:

1. Creates a unique database inside the suite’s PostgreSQL container.
2. Applies the real schema and seed scripts.
3. starts a WebApplicationFactory configured for that database.
4. Executes its HTTP scenario.
5. Disposes the host and drops the database.

This provides a clean deterministic seeded baseline per test and permits parallel execution.

Focused integration scenarios cover successful commands and queries, authorization, Domain errors, pagination, typed EF mappings through round-tripped responses, and concurrency conflicts.

### End-to-end journeys

A single journey runs through HTTP, PostgreSQL, and the worker with a clean database:

1. Full dev → staging → production success, including history, Audit entries, notification, and AI Release Notes.

E2E assertions use public API responses wherever possible. Direct PostgreSQL assertions are reserved for asynchronous effects with no public representation, principally `audit_log` and delivery completion.

No coverage threshold is imposed.

## Documentation and submission

The README will contain:

- Architecture and trade-offs.
- Prerequisites and one-command Docker startup.
- Swagger and health URLs.
- Fixed seed identifiers.
- Copy-paste examples for every command and query.
- State progression and retry examples.
- Database reset and test commands.
- Event-delivery and AI-agent explanations.
- Known limitations and production improvements.

`docs/walkthrough.md` will provide a sub-ten-minute presentation script covering the Domain, invariants, CQRS/use-case flow, transactional events, consumers, ports, AI loop, trade-offs, and next improvements.

`docs/ai-session/README.md` will explain where to place the exported AI collaboration record required by the challenge.

## Implementation order

Each slice should remain working and form a sensible commit boundary:

1. Scaffold the solution, projects, compose topology, and database initialization.
2. Implement typed Domain values, Promotion lifecycle, Domain errors, events, and focused unit tests.
3. Implement EF mappings, repositories, PostgreSQL Event Bus, Unit of Work, schema constraints, and seeds.
4. Implement Request Promotion and the three query paths through handlers and use cases.
5. Implement remaining transition commands, user resolution, error mapping, and Deployment adapter.
6. Implement event delivery claiming, retries, and the Audit and Notification consumers.
7. Implement the mocked model, real agent loop, tools, persistence, and Release Notes consumer in the same worker.
8. Add focused API integration tests and the E2E happy path.
9. Finish Docker verification, Swagger, README, walkthrough, and AI-session handoff.
10. Add GitHub Actions only if core delivery is complete and time remains.

## Completion criteria

The plan is complete when:

- `docker compose up --build` starts PostgreSQL, the API, and the worker.
- Swagger can execute every command and query using documented seed IDs.
- The Promotion aggregate visibly guards every challenge invariant.
- Every committed transition atomically creates its Domain Event and deliveries.
- The Audit consumer records every event after the API response.
- Approved Promotions receive deterministic AI-generated Release Notes.
- Focused tests pass from a clean environment.
- The README and walkthrough explain the chosen trade-offs and omitted production concerns.
