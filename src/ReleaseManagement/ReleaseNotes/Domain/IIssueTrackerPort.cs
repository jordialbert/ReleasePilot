namespace ReleaseManagement.Domain;

public interface IIssueTrackerPort
{
    Task<IReadOnlyList<WorkItem>> GetWorkItems(
        PromotionId promotionId,
        CancellationToken cancellationToken);

    Task<string> AskClarification(
        string workItemId,
        string question,
        CancellationToken cancellationToken);
}
