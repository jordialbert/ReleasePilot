using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public interface IReleaseNotesDraftRepository
{
    Task Add(
        PromotionId promotionId,
        DomainEventId triggeringEventId,
        string content,
        IReadOnlyList<BreakingChange> breakingChanges,
        CancellationToken cancellationToken);
}
