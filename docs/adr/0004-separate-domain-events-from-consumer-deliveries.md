# Separate domain events from consumer deliveries

Each immutable Domain Event is stored once and has one mutable delivery per subscribed consumer, keyed by `(event_id, consumer)`. Deliveries use `pending`, `completed`, and `failed`; a lease and claim token distinguish pending work currently being processed. Workers provide at-least-once delivery, increment attempts when claiming, retry up to five times with exponential delays capped at 30 seconds, and rely on idempotent handlers.
