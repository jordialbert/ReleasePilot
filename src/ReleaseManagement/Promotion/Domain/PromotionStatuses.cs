namespace ReleaseManagement.Domain;

public static class PromotionStatuses
{
    public static bool IsTerminal(this PromotionStatus status) =>
        status is PromotionStatus.Completed
            or PromotionStatus.Cancelled
            or PromotionStatus.RolledBack;
}
