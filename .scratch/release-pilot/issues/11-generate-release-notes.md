# 11 — Generate Release Notes Drafts

**What to build:** Approval triggers a deterministic tool-calling agent that grounds a Markdown Release Notes Draft in linked Work Items and exposes it through Promotion details.

**Blocked by:** 03 — Approve a Promotion; 09 — Audit Domain Events asynchronously.

**Status:** ready-for-agent

**Branch:** `feat/generate-release-notes`

**Proposed commits:**

1. `feat: generate Release Notes Drafts from Work Items`
2. `feat: let the agent clarify ambiguous Work Items`
3. `feat: record Breaking Changes in Release Notes Drafts`
4. `feat: reject invalid or incomplete agent runs`
5. `feat: bound Release Notes generation to ten model turns`

- [ ] Approval creates a Release Notes consumer delivery without blocking the approval response.
- [ ] The generic agent loop validates typed model tool calls, executes them, appends results, and invokes the model again.
- [ ] `GetWorkItems` retrieves deterministic Work Items linked to the Promotion.
- [ ] `AskClarification` retrieves deterministic clarification for a selected Work Item.
- [ ] `FlagBreakingChange` records the Work Item and reason in structured run state.
- [ ] `SubmitReleaseNotes` persists one Markdown draft and its structured Breaking Changes for the Promotion and triggering Event.
- [ ] Promotion details expose the completed draft and remain valid while the draft is absent or still processing.
- [ ] Unknown tools, invalid arguments, model completion without submission, and exceeding ten model turns fail delivery through the normal retry policy.
- [ ] Draft persistence and tool effects are idempotent under repeated delivery.
- [ ] Consumer-level tests cover successful generation, clarification, Breaking Changes, invalid calls, missing submission, and the turn limit.
