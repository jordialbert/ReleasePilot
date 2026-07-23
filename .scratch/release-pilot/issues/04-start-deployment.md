# 04 — Start an idempotent Deployment

**What to build:** A release manager can start Deployment for an Approved Promotion without risking duplicate external execution or persisting a transition when the external system fails.

**Blocked by:** 03 — Approve a Promotion.

**Status:** ready-for-agent

**Branch:** `feat/start-deployment`

**Proposed commits:**

1. `feat: start Deployment for Approved Promotions`
2. `feat: deduplicate Deployment requests by Promotion`
3. `feat: preserve Approved state when Deployment fails`

- [ ] Starting an Approved Promotion invokes the deterministic Deployment adapter and moves the Promotion to Deploying.
- [ ] The successful transition records exactly one `DeploymentStarted` event atomically with the snapshot.
- [ ] The Deployment adapter uses the Promotion ID as its idempotency key and deduplicates repeated external requests.
- [ ] The Deployment call occurs before persistence is committed.
- [ ] An unavailable Deployment system returns safe `503 deployment_unavailable` Problem Details.
- [ ] Deployment failure leaves the stored Promotion Approved and does not persist `DeploymentStarted`.
- [ ] Invalid lifecycle transitions return controlled conflicts.
- [ ] Integration tests prove successful start, idempotency, rollback-on-failure, and safe error disclosure.
