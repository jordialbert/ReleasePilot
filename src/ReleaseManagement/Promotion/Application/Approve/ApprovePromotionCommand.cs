namespace ReleaseManagement.Application;

public sealed record ApprovePromotionCommand(
    Guid PromotionId,
    Guid ActorId);
