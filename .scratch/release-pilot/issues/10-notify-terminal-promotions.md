# 10 — Notify terminal Promotion outcomes

**What to build:** Stakeholders receive an idempotent notification when a Promotion completes, is cancelled, or is rolled back.

**Blocked by:** 05 — Complete a Promotion and advance the Application Version; 06 — Cancel a Promotion and retry its target; 07 — Roll back a Promotion and retry its target; 09 — Audit Domain Events asynchronously.

**Status:** ready-for-agent

**Branch:** `feat/notify-terminal-promotions`

**Proposed commits:**

1. `feat: notify completed Promotions`
2. `feat: notify cancelled Promotions`
3. `feat: notify rolled-back Promotions`
4. `feat: deduplicate terminal notifications by event`

- [ ] Completion creates and processes a notification-consumer delivery.
- [ ] Cancellation creates and processes a notification-consumer delivery.
- [ ] Rollback creates and processes a notification-consumer delivery.
- [ ] Requested, Approved, and DeploymentStarted events do not produce notification deliveries.
- [ ] The deterministic notification adapter receives the Domain Event ID as its idempotency key.
- [ ] Retried at-least-once delivery does not create duplicate external notification effects.
- [ ] Delivery success and failure follow the shared worker acknowledgement and retry behavior.
- [ ] Integration tests cover all subscribed and unsubscribed event types plus idempotent retry.
