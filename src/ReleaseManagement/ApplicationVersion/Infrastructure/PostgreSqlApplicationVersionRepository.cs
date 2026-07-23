using Npgsql;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlApplicationVersionRepository(string connectionString)
    : IApplicationVersionRepository
{
    public async Task<ApplicationVersion?> Find(
        ApplicationVersionId id,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT application_id, label FROM application_versions WHERE id = $1",
            connection);
        command.Parameters.AddWithValue(id.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ApplicationVersion(
            id,
            new ReleaseManagement.Domain.ApplicationId(reader.GetGuid(0)),
            new ApplicationVersionLabel(reader.GetString(1)));
    }
}
