namespace ReleaseManagement.Application;

public sealed record RollbackPromotionCommand(
    Guid PromotionId,
    Guid ActorId);
