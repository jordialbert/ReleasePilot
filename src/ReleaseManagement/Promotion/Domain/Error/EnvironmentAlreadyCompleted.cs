namespace ReleaseManagement.Domain;

public sealed class EnvironmentAlreadyCompleted()
    : DomainException(
        "environment_already_completed",
        "The Application Version has already completed this Environment.");
