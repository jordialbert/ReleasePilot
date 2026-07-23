namespace ReleaseManagement.Application;

public sealed record CompletePromotionCommand(
    Guid PromotionId,
    Guid ActorId);
