namespace ReleaseManagement.Domain;

public interface IApplicationVersionRepository
{
    Task<ApplicationVersion?> Find(
        ApplicationVersionId id,
        CancellationToken cancellationToken);
}
