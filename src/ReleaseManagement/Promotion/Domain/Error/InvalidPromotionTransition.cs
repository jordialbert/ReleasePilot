namespace ReleaseManagement.Domain;

public sealed class InvalidPromotionTransition()
    : DomainException(
        "invalid_promotion_transition",
        "The Promotion cannot make this transition from its current state.");
