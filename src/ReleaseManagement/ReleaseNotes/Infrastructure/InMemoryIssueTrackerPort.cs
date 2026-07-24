using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class InMemoryIssueTrackerPort : IIssueTrackerPort
{
    private static readonly IReadOnlyList<WorkItem> WorkItems =
    [
        new(
            "RP-101",
            "Add deployment health checks",
            "Verify application health before completing a Promotion."),
        new(
            "RP-102",
            "Remove legacy authentication endpoint",
            "Remove the deprecated endpoint after client migration.")
    ];

    public Task<IReadOnlyList<WorkItem>> GetWorkItems(
        PromotionId promotionId,
        CancellationToken cancellationToken) =>
        Task.FromResult(WorkItems);

    public Task<string> AskClarification(
        string workItemId,
        string question,
        CancellationToken cancellationToken)
    {
        if (workItemId != "RP-102")
        {
            throw new InvalidOperationException($"Unknown Work Item: {workItemId}.");
        }

        return Task.FromResult(
            "The v1 authentication endpoint is removed; clients must use v2.");
    }
}
