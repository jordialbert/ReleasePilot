using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlReleaseNotesDraftRepository(
    string connectionString,
    TimeProvider timeProvider)
    : IReleaseNotesDraftRepository
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    public async Task Add(
        PromotionId promotionId,
        DomainEventId triggeringEventId,
        string content,
        IReadOnlyList<BreakingChange> breakingChanges,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO release_notes_drafts (
                id, promotion_id, triggering_event_id, content,
                breaking_changes, created_at
            )
            VALUES ($1, $2, $3, $4, $5, $6)
            -- Replayed deliveries no-op through the unique Promotion and Event constraints.
            ON CONFLICT DO NOTHING
            """,
            connection);
        command.Parameters.AddWithValue(Guid.CreateVersion7());
        command.Parameters.AddWithValue(promotionId.Value);
        command.Parameters.AddWithValue(triggeringEventId.Value);
        command.Parameters.AddWithValue(content);
        command.Parameters.AddWithValue(
            NpgsqlDbType.Jsonb,
            JsonSerializer.Serialize(breakingChanges, Json));
        command.Parameters.AddWithValue(timeProvider.GetUtcNow());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
