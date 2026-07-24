using ReleaseManagement.Domain;

namespace ReleaseManagement.Application;

public interface IAuditLogRepository
{
    Task Add(
        DomainEventId eventId,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken);
}
