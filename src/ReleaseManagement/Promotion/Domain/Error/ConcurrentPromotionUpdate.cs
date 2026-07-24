namespace ReleaseManagement.Domain;

public sealed class ConcurrentPromotionUpdate()
    : DomainException(
        "concurrency_conflict",
        "The Promotion was changed by another request.");
