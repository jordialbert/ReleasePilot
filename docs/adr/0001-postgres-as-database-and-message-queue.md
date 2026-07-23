# Use PostgreSQL as both database and message queue

ReleasePilot will use PostgreSQL 18 for transactional data and a database table as its asynchronous event queue. This keeps the internal tool operationally simple and lets domain changes and queued events commit atomically; it trades away the throughput and delivery features of a dedicated broker, so consumers must explicitly implement claiming, retries, and idempotency.
