# AI collaboration record

These selected conversation exports show how I used AI to plan ReleasePilot,
challenge architectural decisions, implement vertical slices, review feedback,
and verify the resulting behavior.

1. [Architecture planning](1-Planningsession.md) — Defines the Promotion state
   machine, pipeline and concurrency rules, PostgreSQL event queue, API
   contracts, persistence boundaries, Docker topology, and Release Notes agent.
2. [Planning feedback](2-FeedbackAfterPlanningSession.md) — Revisits port
   ownership, simplifies the worker topology, aligns the Release Notes tools
   with the challenge, pins .NET images, and adds Swagger.
3. [Plan to work items](3-TurnPlanIntoWorkItems.md) — Turns the agreed
   architecture into a local specification and 13 dependency-ordered tickets
   with branch names, acceptance criteria, and atomic commit plans.
4. [Cancellation implementation](4-ImplementationIssue6.md) — Implements
   cancellation and same-target retry, then investigates the race between
   cancellation and Deployment start and refactors the transaction seam.
5. [Cancellation review loop](5-FeedbackLoopOnIssue6.md) — Evaluates PR
   feedback, tightens concurrency tests, and unifies Promotion transitions
   behind the row-locked repository operation.
6. [Request Promotion and architecture](6-ImplementationIssue2-definingClearArchitecture.md)
   — Builds the first Promotion API slice, exercises database-enforced
   concurrency, and iteratively settles the controller, module-first, and
   single-project structure.
7. [Start Deployment and trade-offs](7-ImplementationIssue4-architectureTradeOff.md)
   — Implements idempotent Deployment start, analyzes transaction and event
   persistence alternatives, and applies focused review fixes.

The exports preserve the original prompts, responses, implementation detours,
review disagreements, and verification results rather than presenting a
rewritten retrospective.
