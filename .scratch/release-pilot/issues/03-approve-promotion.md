# 03 — Approve a Promotion

**What to build:** An Approver can authorize a Requested Promotion, including one they requested themselves, while Operators receive controlled errors.

**Blocked by:** 02 — Request and inspect a Promotion.

**Status:** ready-for-agent

**Branch:** `feat/approve-promotion`

**Proposed commits:**

1. `feat: approve Requested Promotions`
2. `feat: restrict Promotion approval to Approvers`
3. `feat: reject invalid Promotion approvals`

- [ ] A known Approver can move a Requested Promotion to Approved through the public command endpoint.
- [ ] Self-approval is allowed.
- [ ] Approval records exactly one `PromotionApproved` event in the same transaction as the updated snapshot.
- [ ] A known Operator receives `403 Forbidden` with the stable approval authorization code.
- [ ] Approval from any state other than Requested produces the appropriate controlled Domain conflict.
- [ ] Successful transition commands return `204 No Content`.
- [ ] Promotion details show the Approved snapshot and ordered approval history.
- [ ] Domain and API tests cover the happy path, self-approval, authorization, and invalid transitions.
