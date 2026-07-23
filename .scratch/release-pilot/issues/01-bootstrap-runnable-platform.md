# 01 — Bootstrap a runnable ReleasePilot platform

**What to build:** A production-shaped local ReleasePilot foundation in which the API, worker, and PostgreSQL start together and are ready for vertical feature slices.

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

**Branch:** `chore/bootstrap-runnable-platform`

**Proposed commits:**

1. `chore: scaffold ReleasePilot solution and application hosts`
2. `chore: initialize PostgreSQL schema and seed data`
3. `deploy: build production API and worker images`
4. `deploy: run ReleasePilot with Docker Compose`

- [ ] The solution contains separate Domain, Application, Infrastructure, API, worker, and test projects with the agreed dependency direction.
- [ ] Fixed Operator and Approver users, Applications, and Application Versions are initialized through PostgreSQL schema and seed scripts rather than runtime schema creation.
- [ ] One cache-efficient multi-stage Dockerfile builds either the API or worker from a build argument and runs as the official non-root application user.
- [ ] Docker Compose starts PostgreSQL 18, the API, and the worker with final health dependencies, restart policy, ports, and persistent database volume.
- [ ] The API exposes Swagger and a health endpoint that checks PostgreSQL connectivity and schema initialization.
- [ ] Both application hosts use built-in structured JSON logging and controlled shutdown behavior.
- [ ] A clean checkout can build and start the complete topology with one Docker Compose command.
