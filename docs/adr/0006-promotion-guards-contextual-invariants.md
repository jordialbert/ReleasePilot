# Promotion guards contextual invariants

Promotion owns all lifecycle and progression decisions, including creation. The requesting use case loads the version’s last completed Environment and whether the target slot is active, then passes those facts into `Promotion.Request`; the aggregate validates them and records its event. PostgreSQL’s partial unique index remains the final guard against concurrent requests observing the same slot as free.
