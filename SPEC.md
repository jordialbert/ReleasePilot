# ReleasePilot Specification

## Problem Statement

Release managers need a reliable internal platform for advancing Application Versions through the fixed `dev → staging → production` Environment Pipeline. The platform must prevent invalid or conflicting Promotions, preserve an auditable history of every lifecycle transition, coordinate external Deployment safely, and make terminal outcomes visible without relying on manual tracking.

Release managers also need useful Release Notes Drafts derived from linked Work Items. Producing those drafts manually is slow and risks missing Breaking Changes, while an opaque or non-deterministic automation would be difficult to test and trust.

## Solution

ReleasePilot will provide an HTTP API that manages each Promotion from request through approval, Deployment, and a terminal outcome. The Promotion aggregate will enforce lifecycle, authorization, pipeline progression, active-target uniqueness, and terminal immutability. PostgreSQL will persist current Promotion snapshots and immutable Domain Events atomically, while a worker will use those events to maintain an Audit Log, send terminal notifications, and run a deterministic tool-calling agent that produces Release Notes Drafts.

The platform will expose canonical Promotion details, Application status across all Environments, and paginated Promotion history. It will use deterministic in-memory adapters for external systems, run locally with Docker Compose, and document a complete demonstration workflow.

## User Stories

1. As a release manager, I want to request a Promotion for an Application Version, so that it can begin advancing through the Environment Pipeline.
2. As a release manager, I want the first Promotion for an Application Version to target dev, so that every version enters the pipeline consistently.
3. As a release manager, I want each later Promotion to target exactly the next Environment, so that Application Versions cannot skip required validation.
4. As a release manager, I want a completed Environment to remain recorded as the version's Pipeline Position, so that future progression is based on proven outcomes.
5. As a release manager, I want production completion to end progression for that Application Version, so that no nonexistent Environment can be targeted.
6. As a release manager, I want the platform to reject a Promotion for an Environment the version has already completed, so that successful Deployments are not repeated accidentally.
7. As a release manager, I want the platform to allow a new Promotion after cancellation, so that I can retry the same target Environment with a new attempt.
8. As a release manager, I want the platform to allow a new Promotion after rollback, so that I can retry the same target Environment with a new attempt.
9. As a release manager, I want cancelled and rolled-back attempts to keep their own identities and histories, so that earlier outcomes are never rewritten.
10. As a release manager, I want only one Active Promotion for an Application and target Environment across all its versions, so that competing versions cannot be advanced into the same slot.
11. As a release manager, I want concurrent requests for the same active target to produce one winner and one controlled conflict, so that race conditions cannot violate release policy.
12. As an Approver, I want to approve a Requested Promotion, so that its Deployment can be authorized.
13. As an Approver, I want to approve my own Promotion request, so that small teams are not blocked by a separation-of-duties rule that is outside this scope.
14. As a known non-Approver, I want an approval attempt to be rejected clearly, so that authorization policy is explicit.
15. As a release manager, I want an Approved Promotion to start Deployment, so that the external Deployment system receives the authorized request.
16. As a release manager, I want a Deployment request to use the Promotion ID as its idempotency key, so that retrying after an uncertain response is safe.
17. As a release manager, I want a failed Deployment invocation to leave the stored Promotion Approved, so that external failure does not record a Deployment that was never reliably started.
18. As a release manager, I want an unavailable Deployment system to return a safe service-unavailable response, so that I can distinguish an upstream outage from a business conflict.
19. As a release manager, I want to complete a Deploying Promotion, so that the successful target becomes the Application Version's new Pipeline Position.
20. As a release manager, I want to roll back a Deploying Promotion, so that an unsuccessful in-flight Deployment reaches an explicit terminal outcome.
21. As a release manager, I want to cancel a Requested Promotion, so that unwanted work can stop before approval.
22. As a release manager, I want to cancel an Approved Promotion, so that authorized work can stop before Deployment begins.
23. As a release manager, I want invalid lifecycle transitions to be rejected, so that a Promotion cannot bypass required states.
24. As a release manager, I want Completed, Cancelled, and RolledBack Promotions to be immutable, so that terminal history remains trustworthy.
25. As a release manager, I want each successful transition to append exactly one typed Domain Event, so that downstream processing has a complete and unambiguous source.
26. As an auditor, I want Domain Events to be immutable and ordered, so that I can reconstruct the transition history of a Promotion deterministically.
27. As an auditor, I want an independently consumed Audit Log entry for every Domain Event, so that asynchronous processing is demonstrable and traceable.
28. As a release manager, I want to retrieve a Promotion's canonical details and state history, so that I can understand its current state and how it arrived there.
29. As a release manager, I want Promotion details to include an available Release Notes Draft, so that release communication is visible alongside the Promotion.
30. As a release manager, I want Promotion details to tolerate a Release Notes Draft still being processed, so that asynchronous generation does not block lifecycle queries.
31. As a release manager, I want to see an Application's status for dev, staging, and production, so that the whole Environment Pipeline is visible in one response.
32. As a release manager, I want each Environment status to distinguish the currently deployed version from an Active Promotion, so that stable state and work in progress are not confused.
33. As a release manager, I want cancellation or rollback to leave the currently deployed version unchanged, so that only successful completion changes deployed status.
34. As a release manager, I want paginated Promotion history for an Application, so that I can review attempts without loading an unbounded result.
35. As a release manager, I want Promotion history ordered newest first with deterministic tie-breaking, so that paging is stable.
36. As an API consumer, I want Promotion creation to return the complete canonical representation and its location, so that I can use the created resource immediately.
37. As an API consumer, I want successful lifecycle transitions to return no redundant body, so that command responses remain simple.
38. As an API consumer, I want every command to identify the acting user, so that authorization and event attribution are reliable.
39. As an API consumer, I want missing or unknown users to produce an authentication error, so that unauthenticated actions are distinguishable from forbidden ones.
40. As an API consumer, I want malformed input, missing resources, forbidden actions, conflicts, upstream failures, and unexpected failures to have distinct HTTP statuses, so that clients can respond appropriately.
41. As an API consumer, I want error responses to include stable codes and trace IDs without raw database or upstream details, so that failures are actionable and safe to expose.
42. As an operator, I want Promotion snapshots and their Domain Events committed atomically, so that state and asynchronous work cannot diverge.
43. As an operator, I want each subscribed consumer to have an independent delivery record, so that one consumer's outcome does not corrupt another's progress.
44. As an operator, I want workers to claim deliveries without blocking one another, so that asynchronous processing can scale safely.
45. As an operator, I want delivery claims protected by leases and claim tokens, so that stale workers cannot acknowledge reclaimed work.
46. As an operator, I want failed deliveries retried with bounded exponential delays, so that transient failures recover without creating an uncontrolled retry loop.
47. As an operator, I want a fifth failed attempt to mark a delivery Failed, so that persistent failures become visible and stop consuming worker capacity.
48. As an operator, I want Completed and Failed deliveries retained, so that processing history remains inspectable.
49. As an operator, I want consumer effects and delivery acknowledgement committed together where possible, so that database-side effects remain idempotent.
50. As a stakeholder, I want a notification when a Promotion is Completed, Cancelled, or RolledBack, so that terminal outcomes are visible promptly.
51. As an operator, I want terminal notifications to use the Domain Event ID as an idempotency key, so that at-least-once delivery does not create duplicate effects.
52. As a release manager, I want approval to trigger a Release Notes agent, so that release communication is drafted before the Promotion completes.
53. As a release manager, I want the agent to retrieve Work Items linked to the Promotion, so that the draft is grounded in tracked changes.
54. As a release manager, I want the agent to ask for clarification about a Work Item, so that ambiguous changes can be described accurately.
55. As a release manager, I want the agent to flag Breaking Changes with a reason, so that compatibility risks receive special attention.
56. As a release manager, I want the agent to submit one Markdown Release Notes Draft with structured Breaking Changes, so that both people and software can consume the result.
57. As an operator, I want the agent to validate tool names and arguments, so that malformed model output fails safely.
58. As an operator, I want agent processing to succeed only after draft submission, so that an incomplete conversation is never mistaken for a finished result.
59. As an operator, I want agent processing limited to ten model turns, so that a bad interaction cannot run indefinitely.
60. As a developer, I want deterministic in-memory adapters for Deployment, Work Items, notifications, and the language model, so that the complete architecture can be tested without external accounts.
61. As a developer, I want typed identifiers and business values in the Domain, so that invalid concepts are harder to mix accidentally.
62. As a developer, I want Application Version labels treated as non-empty opaque strings, so that release policy does not depend on semantic-version parsing.
63. As a developer, I want thin CQRS handlers to delegate to named use cases, so that entry points remain explicit while orchestration stays reusable.
64. As a developer, I want query use cases to return consumer-shaped responses, so that controllers never serialize Domain aggregates.
65. As a developer, I want repositories and business-capability ports separated from application-mechanism ports, so that dependencies reflect the Release Management language.
66. As an operator, I want the API, worker, and PostgreSQL to start together through Docker Compose, so that the platform can be demonstrated with one command.
67. As an operator, I want PostgreSQL readiness and schema initialization included in health checks, so that availability reflects the platform's real dependencies.
68. As an API explorer, I want Swagger UI enabled, so that I can discover and execute every command and query.
69. As a developer, I want structured JSON logs with request and processing identifiers, so that HTTP and asynchronous activity can be correlated.
70. As an operator, I want graceful worker shutdown to stop new claims and finish current work when possible, so that restarts minimize duplicate processing.
71. As an evaluator, I want fixed seed data and copy-paste examples, so that I can reproduce the intended workflows quickly.
72. As an evaluator, I want a concise walkthrough of the Domain, architecture, AI loop, and trade-offs, so that I can assess the design in under ten minutes.

## Implementation Decisions

- Release Management is the only bounded context. ReleasePilot is the product and executable grouping.
- The solution will use .NET 10 and PostgreSQL 18, with separate API and worker executables sharing Domain, Application, and Infrastructure projects.
- The Domain has no project dependencies. Application depends on Domain; Infrastructure depends on Application and Domain; the API and worker depend on Application and Infrastructure.
- Commands and queries follow `controller → concrete handler → named use case`. Handlers remain thin, use cases own orchestration, and query use cases map Domain objects to Application responses.
- No MediatR dependency, custom command bus, custom query bus, generic handler abstraction, or generic repository will be introduced.
- The Promotion lifecycle is `Requested → Approved → Deploying → Completed`, with cancellation allowed from Requested or Approved and rollback allowed only from Deploying.
- Completed, Cancelled, and RolledBack are terminal and immutable. Retrying a Cancelled or RolledBack attempt requires a new Promotion identity.
- The fixed Environment Pipeline is `dev → staging → production`. The first target is dev, later targets must immediately follow the last completed Environment, completed Environments cannot be targeted again, and production completion ends progression.
- Only one Active Promotion may exist for an Application and target Environment across every version of that Application. Requested, Approved, and Deploying are active.
- The requesting use case loads contextual facts, while the Promotion factory makes progression and active-target decisions. A partial unique database index closes the concurrent-request race.
- Any known seeded user may request, start, complete, roll back, or cancel. Only an Approver may approve, and self-approval is allowed.
- The Domain uses typed IDs for Promotions, Applications, Application Versions, users, and Domain Events. IDs wrap UUIDv7 values created by the application.
- Application Version labels are validated as non-empty opaque strings and are not parsed as semantic versions.
- Deployment Environment, Promotion Status, and User Role are typed in the Domain and persisted as checked text values.
- Domain failures use specific exceptions with stable public codes for active-target conflicts, skipped Environments, completed Environments, invalid transitions, approval authorization, and terminal immutability.
- Missing resources and malformed HTTP input are application or transport failures, not Domain exceptions.
- Every successful Promotion transition records exactly one typed Domain Event. Promotion status has no public setter.
- Promotions are persisted as current-state snapshots and are not rebuilt from events. Immutable Domain Events provide history and durable asynchronous input without adopting event sourcing.
- Repository interfaces, the Domain Event Bus, and business-capability external ports live in Domain. The Unit of Work and language-model port live in Application. All adapters live in Infrastructure.
- External adapters are deterministic and in-memory. Deployment deduplicates by Promotion ID, notification deduplicates by Event ID, the Work Item adapter returns fixed data and clarifications, and the mocked model returns typed tool calls.
- A command stages the Promotion snapshot and its Domain Events, then commits both in one transaction through a shared scoped database context.
- Named PostgreSQL constraint violations are translated into Domain conflicts. PostgreSQL snapshot concurrency conflicts are translated into controlled concurrency conflicts.
- Starting Deployment applies the transition in memory, invokes the Deployment port with the Promotion ID as the idempotency key, stages state and event, and then commits. Port failure prevents persistence and produces a safe `deployment_unavailable` response.
- PostgreSQL stores users, Applications, Application Versions, Promotions, Domain Events, consumer deliveries, Audit Log entries, and Release Notes Drafts.
- No configurable Environments, current-deployment table, event-sourcing table, separate Promotion-history table, Work Item table, agent-run table, or database migrations will be added.
- Promotion persistence includes identity, Application and version identity, target Environment, status, requester, request time, and completion time. PostgreSQL's implicit row version is the concurrency token and is not exposed in the Domain.
- Database constraints enforce Application/version ownership, unique version labels within an Application, valid persisted enum values, and active-target uniqueness.
- The current deployed version for an Application and Environment is the latest successfully Completed Promotion. Cancellation and rollback do not replace it.
- Domain Events contain identity, deterministic sequence, Promotion identity, type, occurrence time, actor, and event-specific JSON data. Promotion history reads these immutable events directly.
- Each Domain Event is stored once. Each subscribed consumer has a mutable delivery keyed by Event ID and consumer name.
- Deliveries are Pending, Completed, or Failed. Workers claim Pending work with row locking that skips existing claims, increment attempts before handling, and attach a lease and claim token.
- Acknowledgement and failure require the current claim token. Retry delays are 5, 10, 20, and 30 seconds, and the fifth failed attempt is terminal.
- The Audit consumer receives every Domain Event. The notification consumer receives Completed, Cancelled, and RolledBack events. The Release Notes consumer receives Approved events.
- The Audit Log independently copies common event fields and enforces Event ID uniqueness.
- Release Notes Drafts store the Promotion, triggering Event, full Markdown content, structured Breaking Changes, and creation time. Promotion and triggering Event are each unique, and no Breaking Changes is represented by an empty array.
- The API exposes six Promotion commands: request, approve, start Deployment, complete, rollback, and cancel.
- The API exposes three queries: Promotion details, Application status, and paginated Application Promotion history.
- Every command requires an acting user header. Creation returns `201 Created`, the canonical details, and the resource location; other successful transitions return `204 No Content`.
- Promotion details include immutable history and a nullable asynchronous Release Notes Draft.
- Application status always includes dev, staging, and production, distinguishing a currently deployed version from an Active Promotion.
- Promotion history uses one-based page-number pagination, caps page size at 100, reports total count, and orders by request time descending then ID descending.
- Errors use Problem Details with stable `code` and `traceId` values. Public statuses distinguish malformed input, unauthenticated actors, forbidden approval, missing resources, Domain or concurrency conflicts, Deployment unavailability, and unexpected failure.
- Swagger UI is enabled in every environment.
- The Release Notes consumer runs a generic tool-calling loop that validates calls, executes them, appends results to the conversation, and continues until draft submission succeeds.
- Agent tools retrieve linked Work Items, ask for clarification, flag a Breaking Change in run-local structured state, and submit the Release Notes Draft.
- Unknown tools, invalid arguments, reaching completion without submission, or exceeding ten model turns fail the delivery and use the normal retry policy.
- Tool calls are logged structurally and are not persisted in a separate table.
- One multi-stage Dockerfile builds either executable through a build argument, uses official .NET 10 SDK and runtime images, preserves restore caching, and runs as the official non-root user.
- Docker Compose runs PostgreSQL, the API, and the worker. The database initializes from fixed schema and seed scripts, and both executables wait for a healthy initialized database.
- The worker hosts Audit, notification, and Release Notes consumers in one process while keeping each consumer independently extractable.
- The API is exposed on port 8080 with Swagger and health endpoints. Demonstration defaults use a 500 ms poll interval, 60-second lease, five attempts, and a 30-second maximum retry delay.
- Built-in structured JSON logging and `TimeProvider` are used. Logs include trace, Promotion, Event, consumer, attempt, and status identifiers where relevant.
- The health endpoint checks PostgreSQL connectivity and schema initialization. Worker shutdown stops new claims and permits active work to finish within its timeout.
- Documentation will cover architecture, trade-offs, prerequisites, startup, seed identifiers, every API operation, state and retry examples, reset and test commands, event delivery, the AI agent, known limitations, and future production improvements.

## Testing Decisions

- Good tests assert externally observable business behavior rather than implementation details. Trivial getters, passive response shapes, constructors without behavior, framework behavior, and private call sequences are not test targets.
- The highest acceptance seam is one end-to-end journey through HTTP, PostgreSQL, and the worker. It advances one Application Version from dev through staging and production and observes Promotion history, Audit Log effects, terminal notification, Release Notes generation, and completed deliveries.
- End-to-end assertions use public API responses wherever the behavior is represented publicly. Direct PostgreSQL assertions are limited to asynchronous effects with no public representation, principally Audit Log rows and delivery completion.
- Focused Domain tests cover lifecycle success, invalid transitions, progression, skipped Environments, active-target conflicts, approval authorization, terminal immutability, retry eligibility after cancellation or rollback, and emitted Domain Events.
- Focused API integration tests use the public HTTP endpoints to cover successful commands and queries, authentication and approval authorization, stable Domain error responses, pagination, persisted typed-value round trips, Deployment unavailability, and concurrency conflicts.
- API integration tests use the real PostgreSQL 18 schema and seeds through Testcontainers and `WebApplicationFactory`.
- Each integration scenario receives a unique database, applies the production schema and seed scripts, starts an API host configured for that database, and drops the database after the scenario. This creates deterministic isolation and permits parallel execution.
- Queue tests focus on observable claim, lease, claim-token, acknowledgement, retry, terminal-failure, and idempotency behavior where those risks are not fully demonstrated by the end-to-end journey.
- Agent tests operate through the Release Notes consumer and deterministic model adapter, proving valid tool execution and submission as well as failure for unknown tools, invalid arguments, missing submission, and the model-turn limit.
- Time-sensitive tests use the injected `TimeProvider`; they do not depend on wall-clock sleeps.
- Existing architectural decisions are the prior art for test boundaries: Promotion owns Domain decisions, the API owns public command/query behavior, and the worker owns event-consumer behavior.
- No coverage threshold is imposed. Tests are included only when they demonstrate business behavior or meaningful integration risk.

## Out of Scope

- Real authentication, authorization infrastructure, or external-system integrations.
- APIs for managing Applications, Application Versions, Environments, users, or Work Items.
- A configurable Environment Pipeline or skipping Environments.
- Redeploying a successfully completed Application Version to an Environment it has already completed.
- Restoring a previously deployed version after another version has completed an Environment.
- Reopening or mutating Terminal Promotions.
- Database migrations or runtime schema creation.
- A dedicated message broker, queue administration UI, automated retention, or a public replay API.
- Public idempotency-key storage for HTTP commands.
- A frontend.
- A production observability platform.
- Persisted agent transcripts or agent-run records.
- Semantic-version interpretation of Application Version labels.
- Separate worker executables for each consumer.
- GitHub Actions unless all committed scope is complete.

## Further Notes

- The implementation should proceed in vertical slices: scaffold and database initialization; Domain behavior; persistence and transactional events; request and query paths; remaining commands and error handling; worker delivery with Audit and notification; the agent and Release Notes; focused integration and end-to-end tests; then Docker and documentation verification.
- PostgreSQL serves as both the transactional database and asynchronous queue to keep the internal platform operationally simple. This requires explicit claiming, retry, lease, and idempotency behavior.
- Terminal Promotion immutability is deliberate. A future `RedeployVersion` capability may create a new Promotion exempt from the completed-Environment guard, but it is not part of this specification.
- Schema or seed changes require resetting the local PostgreSQL volume before restarting the demonstration stack.
- The exported AI collaboration record required by the challenge will be placed in the documented handoff location; it is not runtime application data.
