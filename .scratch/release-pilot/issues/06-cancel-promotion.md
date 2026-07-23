# 06 — Cancel a Promotion and retry its target

**What to build:** A release manager can cancel a Promotion before Deployment starts and later retry the same target through a new Promotion without rewriting history.

**Blocked by:** 03 — Approve a Promotion.

**Status:** ready-for-agent

**Branch:** `feat/cancel-promotion`

**Architecture constraints:** Use the single `src/ReleaseManagement/ReleaseManagement.csproj`; place business modules directly under `ReleaseManagement` with Domain/Application/Infrastructure inside the owning module; keep one top-level type per matching file; define public routes as topic-controller action methods; keep `Program.cs` route-free; do not retain empty directories.

**Proposed commits:**

1. `feat: cancel Requested Promotions`
2. `feat: cancel Approved Promotions`
3. `feat: retry cancelled Promotion targets`

- [ ] Requested Promotions can be cancelled through the public command endpoint.
- [ ] Approved Promotions can be cancelled before Deployment starts.
- [ ] Each successful cancellation records exactly one `PromotionCancelled` event atomically with the snapshot.
- [ ] Cancelled Promotions are terminal and immutable.
- [ ] Deploying or Completed Promotions cannot be cancelled.
- [ ] Cancellation releases the active target so a new Promotion with a new identity can retry that Environment.
- [ ] The cancelled attempt and retry both remain visible in canonical history.
- [ ] Domain and API tests cover both valid source states, invalid transitions, immutability, and retry.
