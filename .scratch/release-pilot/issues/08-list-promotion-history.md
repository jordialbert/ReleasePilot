# 08 — Browse Application Promotion history

**What to build:** A release manager can browse a stable, bounded, newest-first history of every Promotion attempt for an Application.

**Blocked by:** 02 — Request and inspect a Promotion.

**Status:** ready-for-agent

**Branch:** `feat/list-promotion-history`

**Architecture constraints:** Use the single `src/ReleaseManagement/ReleaseManagement.csproj`; place business modules directly under `ReleaseManagement` with Domain/Application/Infrastructure inside the owning module; keep one top-level type per matching file; define public routes as topic-controller action methods; keep `Program.cs` route-free; do not retain empty directories.

**Proposed commits:**

1. `feat: list the first page of Promotion history`
2. `feat: navigate Promotion history pages`
3. `feat: bound and deterministically order Promotion history`

- [ ] The public query returns Promotions belonging to the requested Application.
- [ ] Results use one-based page-number pagination and report the total count.
- [ ] Callers can request later pages without duplication or omission.
- [ ] Page size is validated and capped at 100.
- [ ] Results are ordered by request time descending and Promotion ID descending.
- [ ] Missing Applications and malformed pagination values return appropriate Problem Details.
- [ ] Query responses are consumer-shaped Application responses rather than serialized Domain aggregates.
- [ ] Real-PostgreSQL API tests cover empty, single-page, multi-page, boundary, and deterministic-order scenarios.
