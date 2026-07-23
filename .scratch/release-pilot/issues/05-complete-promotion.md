# 05 — Complete a Promotion and advance the Application Version

**What to build:** A successful Deployment can complete, become the Application Version's Pipeline Position, and unlock exactly the next Environment in the fixed pipeline.

**Blocked by:** 04.5 — Separate Promotion persistence responsibilities.

**Status:** ready-for-agent

**Branch:** `feat/complete-promotion`

**Architecture constraints:** Use the single `src/ReleaseManagement/ReleaseManagement.csproj`; place business modules directly under `ReleaseManagement` with Domain/Application/Infrastructure inside the owning module; keep one top-level type per matching file; define public routes as topic-controller action methods; keep `Program.cs` route-free; do not retain empty directories.

**Proposed commits:**

1. `feat: complete Deploying Promotions`
2. `feat: show deployed Application versions by Environment`
3. `feat: advance Application Versions to the next Environment`
4. `feat: prevent Promotion beyond completed production`

- [ ] Completing a Deploying Promotion records a Completed snapshot, completion time, and exactly one `PromotionCompleted` event.
- [ ] Completed Promotions are terminal and reject every later transition.
- [ ] Application status always returns dev, staging, and production.
- [ ] Each Environment distinguishes its latest successfully deployed version from any Active Promotion.
- [ ] A completed dev Promotion permits a new Promotion only to staging, and completed staging permits only production.
- [ ] An Environment already completed by the Application Version cannot be targeted again.
- [ ] Completing production ends progression for that Application Version.
- [ ] Concurrent snapshot updates return a controlled conflict rather than a raw database error.
- [ ] Domain and API tests cover completion, status, progression, terminal immutability, and concurrency.
