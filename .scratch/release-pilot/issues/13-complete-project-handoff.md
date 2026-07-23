# 13 — Complete the evaluator handoff

**What to build:** An evaluator can start, exercise, reset, understand, and present ReleasePilot without needing to infer its operation from source code.

**Blocked by:** 12 — Verify the complete release journey.

**Status:** ready-for-agent

**Branch:** `docs/complete-project-handoff`

**Proposed commits:**

1. `docs: explain local startup and database reset`
2. `docs: add Promotion command and query examples`
3. `docs: explain architecture and operational trade-offs`
4. `docs: add walkthrough and AI session handoff`

- [ ] The README documents prerequisites, one-command startup, Swagger, health, fixed seed identifiers, database reset, and test commands.
- [ ] Copy-paste examples cover all six commands and all three queries.
- [ ] Documentation demonstrates successful progression plus cancellation, rollback, retry, pagination, delivery retry, and Release Notes behavior.
- [ ] Architecture notes explain the Promotion aggregate, CQRS/use-case boundary, transactional Domain Events, PostgreSQL delivery queue, consumer idempotency, ports, and agent loop.
- [ ] Trade-offs, known limitations, and realistic production improvements are explicit.
- [ ] A sub-ten-minute walkthrough covers the Domain, invariants, request flow, workers, AI behavior, and next improvements.
- [ ] The AI-session handoff location and expected exported collaboration record are documented.
