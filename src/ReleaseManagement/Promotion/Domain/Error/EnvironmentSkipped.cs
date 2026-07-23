namespace ReleaseManagement.Domain;

public sealed class EnvironmentSkipped()
    : DomainException(
        "environment_skipped",
        "A Promotion must target the next Environment in the pipeline.");
