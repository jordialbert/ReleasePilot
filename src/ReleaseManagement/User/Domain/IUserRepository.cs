namespace ReleaseManagement.Domain;

public interface IUserRepository
{
    Task<Actor?> Find(UserId id, CancellationToken cancellationToken);
}
