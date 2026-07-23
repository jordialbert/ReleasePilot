# 07 — Roll back a Promotion and retry its target

**What to build:** A release manager can roll back a Deploying Promotion without changing the currently deployed version, then retry the target through a new Promotion.

**Blocked by:** 04 — Start an idempotent Deployment.

**Status:** ready-for-agent

**Branch:** `feat/rollback-promotion`

**Proposed commits:**

1. `feat: roll back Deploying Promotions`
2. `feat: preserve deployed versions after rollback`
3. `feat: retry rolled-back Promotion targets`

- [ ] A Deploying Promotion can be rolled back through the public command endpoint.
- [ ] Rollback records exactly one `PromotionRolledBack` event atomically with the snapshot.
- [ ] RolledBack Promotions are terminal and immutable.
- [ ] Promotions in any state other than Deploying cannot be rolled back.
- [ ] Rollback does not replace the latest successfully deployed version shown in Application status.
- [ ] Rollback releases the active target so a new Promotion with a new identity can retry that Environment.
- [ ] The rolled-back attempt and retry both remain visible in canonical history.
- [ ] Domain and API tests cover lifecycle rules, deployed status, immutability, and retry.
