using Npgsql;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlUserRepository(string connectionString) : IUserRepository
{
    public async Task<Actor?> Find(UserId id, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT name, role FROM users WHERE id = $1",
            connection);
        command.Parameters.AddWithValue(id.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var role = reader.GetString(1) switch
        {
            "operator" => UserRole.Operator,
            "approver" => UserRole.Approver,
            _ => throw new InvalidOperationException("Unsupported persisted user role.")
        };
        return new Actor(id, reader.GetString(0), role);
    }
}
