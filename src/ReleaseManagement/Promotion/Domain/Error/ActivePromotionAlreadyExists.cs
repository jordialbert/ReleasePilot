namespace ReleaseManagement.Domain;

public sealed class ActivePromotionAlreadyExists()
    : DomainException(
        "active_promotion_already_exists",
        "An active Promotion already targets this Application and Environment.");
