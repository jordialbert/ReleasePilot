using Npgsql;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlApplicationRepository(string connectionString)
    : IApplicationRepository
{
    public async Task<ReleaseManagement.Domain.Application?> Find(
        ReleaseManagement.Domain.ApplicationId id,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT name FROM applications WHERE id = $1",
            connection);
        command.Parameters.AddWithValue(id.Value);
        var name = await command.ExecuteScalarAsync(cancellationToken);
        if (name is null)
        {
            return null;
        }

        return new ReleaseManagement.Domain.Application(id, (string)name);
    }
}
