namespace ReleaseManagement.Application;

public interface IApplicationStatusReader
{
    Task<ApplicationStatusResponse?> Find(Guid id, CancellationToken cancellationToken);
}
