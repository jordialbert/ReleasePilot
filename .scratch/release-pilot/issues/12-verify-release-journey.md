# 12 — Verify the complete release journey

**What to build:** One high-seam acceptance journey proves that an Application Version can advance through dev, staging, and production while every synchronous and asynchronous outcome remains observable and correct.

**Blocked by:** 08 — Browse Application Promotion history; 10 — Notify terminal Promotion outcomes; 11 — Generate Release Notes Drafts.

**Status:** ready-for-agent

**Branch:** `test/verify-release-journey`

**Proposed commits:**

1. `test: configure isolated end-to-end test databases`
2. `test: verify the complete Environment Pipeline journey`
3. `test: verify asynchronous release effects`

- [ ] End-to-end tests use a real PostgreSQL 18 container and a unique schema-initialized database for each scenario.
- [ ] The acceptance journey drives request, approval, Deployment start, and completion through public HTTP endpoints for dev, staging, and production.
- [ ] Public API assertions verify canonical Promotion details, immutable history, Application status, and paginated Promotion history.
- [ ] The journey observes the asynchronously generated Release Notes Draft through the public Promotion details response.
- [ ] Deterministic notification behavior is asserted through the public adapter seam.
- [ ] Direct PostgreSQL assertions are limited to asynchronous effects with no public representation, principally Audit Log entries and delivery completion.
- [ ] Test isolation permits deterministic parallel execution and cleans up each database after the scenario.
- [ ] The journey does not duplicate lower-seam implementation-detail assertions.
