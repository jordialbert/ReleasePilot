namespace ReleaseManagement.Application;

public sealed record CancelPromotionCommand(
    Guid PromotionId,
    Guid ActorId);
