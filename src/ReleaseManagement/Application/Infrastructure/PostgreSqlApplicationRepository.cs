using Npgsql;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlApplicationRepository(string connectionString)
    : IApplicationRepository
{
    public async Task<bool> Exists(
        ReleaseManagement.Domain.ApplicationId id,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM applications WHERE id = $1)",
            connection);
        command.Parameters.AddWithValue(id.Value);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
