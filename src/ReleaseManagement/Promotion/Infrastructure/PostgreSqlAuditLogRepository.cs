using Npgsql;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlAuditLogRepository(string connectionString)
    : IAuditLogRepository
{
    public async Task Add(
        DomainEventId eventId,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO audit_log (
                event_id, type, promotion_id, occurred_at, actor_id, recorded_at
            )
            SELECT id, type, promotion_id, occurred_at, actor_id, $2
            FROM domain_events
            WHERE id = $1
            ON CONFLICT (event_id) DO NOTHING
            """,
            connection);
        command.Parameters.AddWithValue(eventId.Value);
        command.Parameters.AddWithValue(recordedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
