namespace ReleaseManagement.Domain;

public sealed record TerminalPromotionNotification(
    PromotionId PromotionId,
    PromotionStatus Outcome);
