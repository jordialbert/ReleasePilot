using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

internal static class PromotionStatusSql
{
    /// <summary>
    /// A SQL value list of every non-terminal status,
    /// e.g. <c>'requested', 'approved', 'deploying'</c>.
    /// </summary>
    public static readonly string ActiveList = string.Join(
        ", ",
        Enum.GetValues<PromotionStatus>()
            .Where(status => !status.IsTerminal())
            .Select(status => $"'{Name(status)}'"));

    public static string Name(PromotionStatus status) => status switch
    {
        PromotionStatus.Requested => "requested",
        PromotionStatus.Approved => "approved",
        PromotionStatus.Deploying => "deploying",
        PromotionStatus.Completed => "completed",
        PromotionStatus.Cancelled => "cancelled",
        PromotionStatus.RolledBack => "rolled_back",
        _ => throw new InvalidOperationException("Unsupported Promotion status.")
    };

    public static PromotionStatus Parse(string name) => name switch
    {
        "requested" => PromotionStatus.Requested,
        "approved" => PromotionStatus.Approved,
        "deploying" => PromotionStatus.Deploying,
        "completed" => PromotionStatus.Completed,
        "cancelled" => PromotionStatus.Cancelled,
        "rolled_back" => PromotionStatus.RolledBack,
        _ => throw new InvalidOperationException("Unsupported persisted Promotion status.")
    };
}
