using Npgsql;
using ReleaseManagement.Application;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlApplicationStatusReader(string connectionString)
    : IApplicationStatusReader
{
    public async Task<ApplicationStatusResponse?> Find(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT environment.name,
                   deployed.version_id, deployed.label, deployed.completed_at,
                   active.id, active.version_id, active.label, active.status
            FROM applications application
            CROSS JOIN (
                VALUES ('dev', 1), ('staging', 2), ('production', 3)
            ) environment(name, position)
            LEFT JOIN LATERAL (
                SELECT version.id AS version_id, version.label, promotion.completed_at
                FROM promotions promotion
                JOIN application_versions version
                  ON version.id = promotion.application_version_id
                WHERE promotion.application_id = application.id
                  AND promotion.target_environment = environment.name
                  AND promotion.status = 'completed'
                ORDER BY promotion.completed_at DESC, promotion.id DESC
                LIMIT 1
            ) deployed ON true
            LEFT JOIN LATERAL (
                SELECT promotion.id, version.id AS version_id,
                       version.label, promotion.status
                FROM promotions promotion
                JOIN application_versions version
                  ON version.id = promotion.application_version_id
                WHERE promotion.application_id = application.id
                  AND promotion.target_environment = environment.name
                  AND promotion.status IN ('requested', 'approved', 'deploying')
                LIMIT 1
            ) active ON true
            WHERE application.id = $1
            ORDER BY environment.position
            """,
            connection);
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var environments = new List<ApplicationEnvironmentStatusResponse>();
        while (await reader.ReadAsync(cancellationToken))
        {
            DeployedVersionResponse? deployedVersion = null;
            if (!reader.IsDBNull(1))
            {
                deployedVersion = new DeployedVersionResponse(
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetFieldValue<DateTimeOffset>(3));
            }

            ActivePromotionResponse? activePromotion = null;
            if (!reader.IsDBNull(4))
            {
                activePromotion = new ActivePromotionResponse(
                    reader.GetGuid(4),
                    reader.GetGuid(5),
                    reader.GetString(6),
                    reader.GetString(7));
            }

            environments.Add(new ApplicationEnvironmentStatusResponse(
                reader.GetString(0),
                deployedVersion,
                activePromotion));
        }

        if (environments.Count == 0)
        {
            return null;
        }

        return new ApplicationStatusResponse(id, environments);
    }
}
