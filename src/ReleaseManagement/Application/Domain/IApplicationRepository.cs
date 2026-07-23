namespace ReleaseManagement.Domain;

public interface IApplicationRepository
{
    Task<Application?> Find(
        ApplicationId id,
        CancellationToken cancellationToken);
}
