# ReleasePilot

ReleasePilot is an internal backend platform that advances application versions
through the fixed `dev → staging → production` pipeline. Its core is a
`Promotion` aggregate that owns lifecycle and pipeline invariants. A thin HTTP
API executes commands and queries, PostgreSQL stores aggregate snapshots and
Domain Events atomically, and a separate worker consumes durable event
deliveries for audit, terminal notifications, and deterministic AI-generated
Release Notes.

The [project walkthrough](docs/walkthrough.md) explains the Domain model,
architecture, decisions, trade-offs, limitations, and production improvements.
The [AI-session directory](docs/ai-session/README.md) is the handoff location
for the collaboration record required by the challenge.

## Run locally

### Prerequisites

- Docker with Docker Compose v2
- `curl` and `jq` for the examples below
- .NET SDK 10 only when running the tests outside Docker

Start PostgreSQL, the API, and the worker:

```bash
docker compose up --build --wait
```

Once the services are ready:

- Swagger UI: [http://localhost:8080/docs](http://localhost:8080/docs)
- Health: [http://localhost:8080/health](http://localhost:8080/health)
- The root URL redirects to Swagger UI.

Verify health:

```bash
curl --fail http://localhost:8080/health
```

The expected response is `Healthy`.

Follow the API and worker logs:

```bash
docker compose logs --follow api worker
```

API entries show the HTTP method, path, query, response status, duration, and
JSON request and response bodies up to 4 KiB. Request headers are not logged.
Worker entries show the consumer, Domain Event, Promotion, and attempt for every
completed asynchronous delivery.

Stop the platform without deleting data:

```bash
docker compose down
```

### Reset the database

The schema and fixed seed data are applied only when PostgreSQL creates a fresh
volume. Delete that volume to reset the demonstration:

```bash
docker compose down --volumes
docker compose up --build --wait
```

## Fixed seed data

Commands identify the acting user with the `X-User-Id` header. This is a
deliberately small authentication seam, not production authentication.

| Type | Name or label | Role | ID |
| --- | --- | --- | --- |
| User | Alex Approver | approver | `01900000-0000-7000-8000-000000000001` |
| User | Riley Operator | operator | `01900000-0000-7000-8000-000000000002` |
| Application | Checkout Service | — | `01900000-0000-7000-8000-000000000101` |
| Application | Customer Portal | — | `01900000-0000-7000-8000-000000000102` |
| Checkout version | `2026.7.1` | — | `01900000-0000-7000-8000-000000000201` |
| Checkout version | `release-candidate` | — | `01900000-0000-7000-8000-000000000202` |
| Customer Portal version | `42` | — | `01900000-0000-7000-8000-000000000203` |

Set variables once before running the examples:

```bash
BASE_URL=http://localhost:8080
APPROVER_ID=01900000-0000-7000-8000-000000000001
OPERATOR_ID=01900000-0000-7000-8000-000000000002
APPLICATION_ID=01900000-0000-7000-8000-000000000101
VERSION_ID=01900000-0000-7000-8000-000000000201
```

## Complete release journey

This copy-paste journey exercises all six commands and all three queries. It
completes `dev`, demonstrates cancellation and rollback while targeting
`staging`, retries both terminal attempts, and then completes `staging` and
`production`.

Every command requires `X-User-Id`. Request returns `201 Created` with canonical
Promotion details. The other five successful commands return `204 No Content`.
Errors use Problem Details with stable `code` and `traceId` fields.

### 1. Complete dev

Request a Promotion:

```bash
DEV_PROMOTION_ID=$(
  curl --silent --fail \
    --request POST "$BASE_URL/promotions" \
    --header "Content-Type: application/json" \
    --header "X-User-Id: $OPERATOR_ID" \
    --data "{
      \"applicationVersionId\": \"$VERSION_ID\",
      \"targetEnvironment\": \"dev\"
    }" |
  jq --raw-output '.id'
)

echo "$DEV_PROMOTION_ID"
```

Approve it. Approval is the only command restricted to an approver:

```bash
curl --silent --fail \
  --request POST "$BASE_URL/promotions/$DEV_PROMOTION_ID/approve" \
  --header "X-User-Id: $APPROVER_ID"
```

`PromotionApproved` queues Release Notes generation. The worker uses a
deterministic language model, so the result is reproducible:

```bash
sleep 1
curl --silent --fail "$BASE_URL/promotions/$DEV_PROMOTION_ID" |
  jq '.releaseNotesDraft'
```

The draft mentions work items `RP-101` and `RP-102`, and flags `RP-102` as a
Breaking Change.

Start Deployment and complete the Promotion:

```bash
curl --silent --fail \
  --request POST "$BASE_URL/promotions/$DEV_PROMOTION_ID/start-deployment" \
  --header "X-User-Id: $OPERATOR_ID"

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$DEV_PROMOTION_ID/complete" \
  --header "X-User-Id: $OPERATOR_ID"
```

Read the Promotion details and immutable event history:

```bash
curl --silent --fail "$BASE_URL/promotions/$DEV_PROMOTION_ID" | jq
```

Read the Application status. It always contains `dev`, `staging`, and
`production`, separating deployed versions from active Promotions:

```bash
curl --silent --fail "$BASE_URL/applications/$APPLICATION_ID/status" | jq
```

### 2. Cancel staging and retry

Request and cancel a staging Promotion:

```bash
CANCELLED_STAGING_ID=$(
  curl --silent --fail \
    --request POST "$BASE_URL/promotions" \
    --header "Content-Type: application/json" \
    --header "X-User-Id: $OPERATOR_ID" \
    --data "{
      \"applicationVersionId\": \"$VERSION_ID\",
      \"targetEnvironment\": \"staging\"
    }" |
  jq --raw-output '.id'
)

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$CANCELLED_STAGING_ID/cancel" \
  --header "X-User-Id: $OPERATOR_ID"
```

Cancellation leaves the deployed dev version unchanged and releases the active
staging slot. A new Promotion with a new identity can retry that same target.

### 3. Roll back staging and retry

Request the first retry, approve it, start Deployment, and roll it back:

```bash
ROLLED_BACK_STAGING_ID=$(
  curl --silent --fail \
    --request POST "$BASE_URL/promotions" \
    --header "Content-Type: application/json" \
    --header "X-User-Id: $OPERATOR_ID" \
    --data "{
      \"applicationVersionId\": \"$VERSION_ID\",
      \"targetEnvironment\": \"staging\"
    }" |
  jq --raw-output '.id'
)

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$ROLLED_BACK_STAGING_ID/approve" \
  --header "X-User-Id: $APPROVER_ID"

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$ROLLED_BACK_STAGING_ID/start-deployment" \
  --header "X-User-Id: $OPERATOR_ID"

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$ROLLED_BACK_STAGING_ID/rollback" \
  --header "X-User-Id: $OPERATOR_ID"
```

Rollback also leaves the deployed dev version unchanged and permits another
staging attempt.

### 4. Complete staging

```bash
STAGING_PROMOTION_ID=$(
  curl --silent --fail \
    --request POST "$BASE_URL/promotions" \
    --header "Content-Type: application/json" \
    --header "X-User-Id: $OPERATOR_ID" \
    --data "{
      \"applicationVersionId\": \"$VERSION_ID\",
      \"targetEnvironment\": \"staging\"
    }" |
  jq --raw-output '.id'
)

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$STAGING_PROMOTION_ID/approve" \
  --header "X-User-Id: $APPROVER_ID"

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$STAGING_PROMOTION_ID/start-deployment" \
  --header "X-User-Id: $OPERATOR_ID"

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$STAGING_PROMOTION_ID/complete" \
  --header "X-User-Id: $OPERATOR_ID"
```

### 5. Complete production

```bash
PRODUCTION_PROMOTION_ID=$(
  curl --silent --fail \
    --request POST "$BASE_URL/promotions" \
    --header "Content-Type: application/json" \
    --header "X-User-Id: $OPERATOR_ID" \
    --data "{
      \"applicationVersionId\": \"$VERSION_ID\",
      \"targetEnvironment\": \"production\"
    }" |
  jq --raw-output '.id'
)

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$PRODUCTION_PROMOTION_ID/approve" \
  --header "X-User-Id: $APPROVER_ID"

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$PRODUCTION_PROMOTION_ID/start-deployment" \
  --header "X-User-Id: $OPERATOR_ID"

curl --silent --fail \
  --request POST "$BASE_URL/promotions/$PRODUCTION_PROMOTION_ID/complete" \
  --header "X-User-Id: $OPERATOR_ID"

curl --silent --fail "$BASE_URL/applications/$APPLICATION_ID/status" | jq
```

The final status shows version `2026.7.1` deployed in all three environments.

### 6. Browse paginated Promotion history

The list is ordered by request time descending, then Promotion ID descending.
The first response establishes a snapshot so later pages stay stable if new
Promotions are created concurrently.

```bash
PAGE_ONE=$(
  curl --silent --fail \
    "$BASE_URL/applications/$APPLICATION_ID/promotions?page=1&pageSize=2"
)

echo "$PAGE_ONE" | jq

SNAPSHOT_REQUESTED_AT=$(echo "$PAGE_ONE" | jq --raw-output '.snapshot.requestedAt')
SNAPSHOT_PROMOTION_ID=$(echo "$PAGE_ONE" | jq --raw-output '.snapshot.promotionId')

curl --silent --fail --get \
  "$BASE_URL/applications/$APPLICATION_ID/promotions" \
  --data-urlencode "page=2" \
  --data-urlencode "pageSize=2" \
  --data-urlencode "snapshotRequestedAt=$SNAPSHOT_REQUESTED_AT" \
  --data-urlencode "snapshotPromotionId=$SNAPSHOT_PROMOTION_ID" |
jq
```

Page numbers are one-based and page size is limited to 100. Requests after page
one must send both snapshot values.

## Observe asynchronous delivery

The command transaction writes the Promotion snapshot, Domain Event, and
consumer delivery rows together. The API responds without waiting for the
worker. Inspect delivery completion and retry attempts:

```bash
docker compose exec --no-TTY db \
  psql --username releasepilot --dbname releasepilot \
  --command "
    SELECT consumer, status, attempts, count(*)
    FROM event_deliveries
    GROUP BY consumer, status, attempts
    ORDER BY consumer, status, attempts;
  "
```

Inspect the independently persisted Audit Log:

```bash
docker compose exec --no-TTY db \
  psql --username releasepilot --dbname releasepilot \
  --command "
    SELECT type, promotion_id, actor_id, occurred_at, recorded_at
    FROM audit_log
    ORDER BY occurred_at, event_id;
  "
```

Deliveries use a 60-second lease and claim token. Failed handling is retried
after 5, 10, 20, and 30 seconds; the fifth failure is terminal. The focused
integration test demonstrates the entire retry schedule without wall-clock
waiting:

```bash
dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj \
  --filter "FullyQualifiedName~AuditEventDeliveryTests.RetriesAtConfiguredTimesAndFailsTheFifthAttempt"
```

Consumer idempotency is based on the Domain Event ID. Audit rows are unique by
Event ID, notifications receive it as their idempotency key, and Release Notes
Drafts are unique by Promotion and triggering Event.

## Tests

Docker must be running because integration tests create isolated PostgreSQL 18
containers. Run the focused test projects:

```bash
dotnet test tests/ReleaseManagement.Domain.Tests/ReleaseManagement.Domain.Tests.csproj
dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj
```

Run only the deterministic Release Notes scenario:

```bash
dotnet test tests/ReleasePilot.IntegrationTests/ReleasePilot.IntegrationTests.csproj \
  --filter "FullyQualifiedName~ReleaseNotesEventDeliveryTests.PersistsOneDraftAndExposesItThroughPromotionDetails"
```

## Further documentation

- [Project walkthrough](docs/walkthrough.md)
- [Architecture Decision Records](docs/adr/)
- [AI collaboration record](docs/ai-session/README.md)
- [Specification](SPEC.md)
- [Context](CONTEXT.md)
