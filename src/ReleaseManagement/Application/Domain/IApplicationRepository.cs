namespace ReleaseManagement.Domain;

public interface IApplicationRepository
{
    Task<bool> Exists(ApplicationId id, CancellationToken cancellationToken);
}
