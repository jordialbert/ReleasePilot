# Persist aggregate snapshots and domain events

Promotions are stored as current-state snapshots and are not rebuilt from events. Each state transition appends an immutable domain event in the same transaction; those events provide Promotion history and the durable input for asynchronous consumers without introducing event-sourcing complexity.
