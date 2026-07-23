using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

internal static class PostgreSqlPromotionEventWriter
{
    public static async Task Write(
        PromotionDomainEvent domainEvent,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var persistence = domainEvent switch
        {
            PromotionRequested requested => (
                Type: "promotion_requested",
                Payload: JsonSerializer.Serialize(new
                {
                    applicationId = requested.ApplicationId.Value,
                    applicationVersionId = requested.ApplicationVersionId.Value,
                    targetEnvironment =
                        requested.TargetEnvironment.ToString().ToLowerInvariant()
                }),
                Consumers: new[] { "audit" }),
            PromotionApproved => (
                Type: "promotion_approved",
                Payload: "{}",
                Consumers: new[] { "audit", "release_notes" }),
            DeploymentStarted => (
                Type: "deployment_started",
                Payload: "{}",
                Consumers: new[] { "audit" }),
            PromotionCompleted => (
                Type: "promotion_completed",
                Payload: "{}",
                Consumers: new[] { "audit", "notification" }),
            _ => throw new UnreachableException()
        };
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO domain_events (
                id, promotion_id, type, occurred_at, actor_id, payload
            )
            VALUES ($1, $2, $3, $4, $5, $6)
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(domainEvent.Id.Value);
            command.Parameters.AddWithValue(domainEvent.PromotionId.Value);
            command.Parameters.AddWithValue(persistence.Type);
            command.Parameters.AddWithValue(domainEvent.OccurredAt);
            command.Parameters.AddWithValue(domainEvent.ActorId.Value);
            command.Parameters.AddWithValue(NpgsqlDbType.Jsonb, persistence.Payload);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO event_deliveries (event_id, consumer, available_at)
            SELECT $1, consumer, $2
            FROM unnest($3::text[]) AS consumer
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(domainEvent.Id.Value);
            command.Parameters.AddWithValue(domainEvent.OccurredAt);
            command.Parameters.AddWithValue(persistence.Consumers);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
