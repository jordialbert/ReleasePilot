using Npgsql;
using ReleaseManagement.Application;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlPromotionListReader(string connectionString)
    : IPromotionListReader
{
    public async Task<PromotionListPageResponse> List(
        ListPromotionsQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var snapshot = query.Snapshot;
        if (snapshot is null)
        {
            await using var snapshotCommand = new NpgsqlCommand(
                """
                SELECT requested_at, id
                FROM promotions
                WHERE application_id = $1
                ORDER BY requested_at DESC, id DESC
                LIMIT 1
                """,
                connection);
            snapshotCommand.Parameters.AddWithValue(query.ApplicationId);
            await using var snapshotReader =
                await snapshotCommand.ExecuteReaderAsync(cancellationToken);
            if (await snapshotReader.ReadAsync(cancellationToken))
            {
                snapshot = new PromotionListSnapshot(
                    snapshotReader.GetFieldValue<DateTimeOffset>(0),
                    snapshotReader.GetGuid(1));
            }
        }

        if (snapshot is null)
        {
            return new PromotionListPageResponse(
                [],
                query.Page,
                query.PageSize,
                0,
                null);
        }

        await using var countCommand = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM promotions
            WHERE application_id = $1
              AND (requested_at, id) <= ($2, $3)
            """,
            connection);
        countCommand.Parameters.AddWithValue(query.ApplicationId);
        countCommand.Parameters.AddWithValue(snapshot.RequestedAt);
        countCommand.Parameters.AddWithValue(snapshot.PromotionId);
        var totalCount =
            (long)(await countCommand.ExecuteScalarAsync(cancellationToken))!;

        var items = new List<PromotionSummaryResponse>();
        await using var command = new NpgsqlCommand(
            """
            SELECT p.id, p.application_id, p.application_version_id, av.label,
                   p.target_environment, p.status, p.requested_by,
                   p.requested_at, p.completed_at
            FROM promotions p
            JOIN application_versions av ON av.id = p.application_version_id
            WHERE p.application_id = $1
              AND (p.requested_at, p.id) <= ($4, $5)
            ORDER BY p.requested_at DESC, p.id DESC
            LIMIT $2 OFFSET $3
            """,
            connection);
        command.Parameters.AddWithValue(query.ApplicationId);
        command.Parameters.AddWithValue(query.PageSize);
        command.Parameters.AddWithValue(((long)query.Page - 1) * query.PageSize);
        command.Parameters.AddWithValue(snapshot.RequestedAt);
        command.Parameters.AddWithValue(snapshot.PromotionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            DateTimeOffset? completedAt = null;
            if (!reader.IsDBNull(8))
            {
                completedAt = reader.GetFieldValue<DateTimeOffset>(8);
            }

            items.Add(new PromotionSummaryResponse(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetGuid(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                completedAt));
        }

        return new PromotionListPageResponse(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            snapshot);
    }
}
