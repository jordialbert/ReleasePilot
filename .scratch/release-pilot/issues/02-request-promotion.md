# 02 — Request and inspect a Promotion

**What to build:** A known user can request the first dev Promotion for an Application Version and immediately inspect its canonical state and immutable history.

**Blocked by:** 01 — Bootstrap a runnable ReleasePilot platform.

**Status:** ready-for-agent

**Branch:** `feat/request-promotion`

**Architecture constraints:** Use the single `src/ReleaseManagement/ReleaseManagement.csproj`; place business modules directly under `ReleaseManagement` with Domain/Application/Infrastructure inside the owning module; keep one top-level type per matching file; define public routes as topic-controller action methods; keep `Program.cs` route-free; do not retain empty directories.

**Proposed commits:**

1. `feat: request and inspect dev Promotions`
2. `feat: reject invalid initial Promotion targets`
3. `feat: prevent competing Active Promotions`

- [ ] Requesting a valid first Promotion returns `201 Created`, the canonical Promotion details, and its resource location.
- [ ] Promotion details expose the current snapshot, deterministic immutable history, and a nullable Release Notes Draft.
- [ ] Every successful request records exactly one `PromotionRequested` Domain Event in the same transaction as the Promotion snapshot.
- [ ] Typed identifiers, actor, version label, Environment, status, and Domain errors enforce the agreed Domain vocabulary.
- [ ] A first Promotion targeting anything other than dev is rejected with a stable conflict code.
- [ ] A second Active Promotion for the same Application and target Environment is rejected, including under concurrent requests.
- [ ] Missing or unknown actors, malformed input, and missing resources return safe Problem Details with stable codes and trace IDs.
- [ ] Focused Domain and real-PostgreSQL API tests assert behavior through the aggregate and HTTP seams.
