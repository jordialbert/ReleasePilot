namespace ReleaseManagement.Domain;

public sealed class TerminalPromotionIsImmutable()
    : DomainException(
        "terminal_promotion_is_immutable",
        "A terminal Promotion cannot transition again.");
