# Separate CQRS handlers from use cases

Every command and query has a thin Application handler that delegates to a named Application use case. Use cases coordinate Domain objects and repositories, while query use cases also map Domain objects into consumer-shaped responses. Controllers call concrete handlers directly without MediatR or a command/query bus; the separation is retained to keep CQRS entry points distinct from reusable application behavior.
