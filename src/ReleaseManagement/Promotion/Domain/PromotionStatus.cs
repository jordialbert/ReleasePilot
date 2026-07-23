namespace ReleaseManagement.Domain;

public enum PromotionStatus
{
    Requested,
    Approved,
    Deploying,
    Completed,
    Cancelled,
    RolledBack
}
