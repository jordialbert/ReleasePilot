# 09 — Audit Domain Events asynchronously

**What to build:** Every immutable Promotion Domain Event is delivered independently through PostgreSQL and recorded once in the Audit Log.

**Blocked by:** 02 — Request and inspect a Promotion.

**Status:** ready-for-agent

**Branch:** `feat/audit-promotion-events`

**Architecture constraints:** Use the single `src/ReleaseManagement/ReleaseManagement.csproj`; place business modules directly under `ReleaseManagement` with Domain/Application/Infrastructure inside the owning module; keep one top-level type per matching file; define public routes as topic-controller action methods; keep `Program.cs` route-free; do not retain empty directories.

**Proposed commits:**

1. `feat: audit Promotion events asynchronously`
2. `feat: protect event delivery claims with leases`
3. `feat: retry failed event deliveries`
4. `feat: make Audit Log consumption idempotent`

- [ ] Publishing a Domain Event creates the Audit consumer delivery in the same transaction as the aggregate snapshot and event.
- [ ] The worker claims available deliveries without blocking another worker's claims.
- [ ] Claiming increments attempts and assigns a lease and unique claim token before handler invocation.
- [ ] Only the current claim token can acknowledge or fail a delivery.
- [ ] Failed handling retries after 5, 10, 20, and 30 seconds using the injected `TimeProvider`.
- [ ] A fifth failed attempt marks the delivery Failed, while successful processing marks it Completed.
- [ ] Every Domain Event produces one Audit Log entry with the agreed event metadata.
- [ ] Audit Log writes are idempotent by Event ID.
- [ ] Completed and Failed deliveries remain persisted for inspection.
- [ ] Queue tests cover concurrent claims, stale tokens, lease expiry, retries, terminal failure, and idempotency without wall-clock sleeps.
