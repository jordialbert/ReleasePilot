using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;

namespace ReleaseManagement.Infrastructure;

public sealed class PostgreSqlPromotionRepository(string connectionString)
    : IPromotionRepository, IPromotionDetailsReader
{
    public async Task<Actor?> FindActor(UserId id, CancellationToken cancellationToken)
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

    public async Task<ApplicationVersion?> FindApplicationVersion(
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

    public async Task<DeploymentEnvironment?> FindLastCompletedEnvironment(
        ApplicationVersionId id,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT target_environment
            FROM promotions
            WHERE application_version_id = $1 AND status = 'completed'
            ORDER BY CASE target_environment
                WHEN 'production' THEN 3
                WHEN 'staging' THEN 2
                ELSE 1
            END DESC
            LIMIT 1
            """,
            connection);
        command.Parameters.AddWithValue(id.Value);
        var value = await command.ExecuteScalarAsync(cancellationToken);

        return value switch
        {
            null => null,
            "dev" => DeploymentEnvironment.Dev,
            "staging" => DeploymentEnvironment.Staging,
            "production" => DeploymentEnvironment.Production,
            _ => throw new InvalidOperationException("Unsupported persisted Environment.")
        };
    }

    public async Task<bool> HasActivePromotion(
        ReleaseManagement.Domain.ApplicationId applicationId,
        DeploymentEnvironment targetEnvironment,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM promotions
                WHERE application_id = $1
                  AND target_environment = $2
                  AND status IN ('requested', 'approved', 'deploying')
            )
            """,
            connection);
        command.Parameters.AddWithValue(applicationId.Value);
        command.Parameters.AddWithValue(targetEnvironment.ToString().ToLowerInvariant());
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task Add(Promotion promotion, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var command = new NpgsqlCommand(
                """
                INSERT INTO promotions (
                    id, application_id, application_version_id, target_environment,
                    status, requested_by, requested_at
                )
                VALUES ($1, $2, $3, $4, 'requested', $5, $6)
                """,
                connection,
                transaction))
            {
                command.Parameters.AddWithValue(promotion.Id.Value);
                command.Parameters.AddWithValue(promotion.ApplicationId.Value);
                command.Parameters.AddWithValue(promotion.ApplicationVersionId.Value);
                command.Parameters.AddWithValue(
                    promotion.TargetEnvironment.ToString().ToLowerInvariant());
                command.Parameters.AddWithValue(promotion.RequestedBy.Value);
                command.Parameters.AddWithValue(promotion.RequestedAt);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var domainEvent = promotion.RequestedEvent!;
            var payload = JsonSerializer.Serialize(new
            {
                applicationId = domainEvent.ApplicationId.Value,
                applicationVersionId = domainEvent.ApplicationVersionId.Value,
                targetEnvironment =
                    domainEvent.TargetEnvironment.ToString().ToLowerInvariant()
            });
            await using (var command = new NpgsqlCommand(
                """
                INSERT INTO domain_events (
                    id, promotion_id, type, occurred_at, actor_id, payload
                )
                VALUES ($1, $2, 'promotion_requested', $3, $4, $5)
                """,
                connection,
                transaction))
            {
                command.Parameters.AddWithValue(domainEvent.Id.Value);
                command.Parameters.AddWithValue(domainEvent.PromotionId.Value);
                command.Parameters.AddWithValue(domainEvent.OccurredAt);
                command.Parameters.AddWithValue(domainEvent.ActorId.Value);
                command.Parameters.AddWithValue(
                    NpgsqlDbType.Jsonb,
                    payload);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = new NpgsqlCommand(
                """
                INSERT INTO event_deliveries (event_id, consumer, available_at)
                VALUES ($1, 'audit', $2)
                """,
                connection,
                transaction))
            {
                command.Parameters.AddWithValue(domainEvent.Id.Value);
                command.Parameters.AddWithValue(domainEvent.OccurredAt);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception)
            when (exception.ConstraintName == "promotions_active_target")
        {
            throw new ActivePromotionAlreadyExists();
        }
    }

    public async Task<Promotion?> Find(
        PromotionId id,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT application_id, application_version_id, target_environment,
                   status, requested_by, requested_at
            FROM promotions
            WHERE id = $1
            """,
            connection);
        command.Parameters.AddWithValue(id.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var targetEnvironment = reader.GetString(2) switch
        {
            "dev" => DeploymentEnvironment.Dev,
            "staging" => DeploymentEnvironment.Staging,
            "production" => DeploymentEnvironment.Production,
            _ => throw new InvalidOperationException("Unsupported persisted Environment.")
        };
        var status = reader.GetString(3) switch
        {
            "requested" => PromotionStatus.Requested,
            "approved" => PromotionStatus.Approved,
            "deploying" => PromotionStatus.Deploying,
            "completed" => PromotionStatus.Completed,
            "cancelled" => PromotionStatus.Cancelled,
            "rolled_back" => PromotionStatus.RolledBack,
            _ => throw new InvalidOperationException("Unsupported persisted Promotion status.")
        };
        return new Promotion(
            id,
            new ReleaseManagement.Domain.ApplicationId(reader.GetGuid(0)),
            new ApplicationVersionId(reader.GetGuid(1)),
            targetEnvironment,
            status,
            new UserId(reader.GetGuid(4)),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    public async Task Update(Promotion promotion, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = new NpgsqlCommand(
            """
            UPDATE promotions
            SET status = 'approved'
            WHERE id = $1 AND status = 'requested'
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(promotion.Id.Value);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new InvalidPromotionTransition();
            }
        }
        var domainEvent = promotion.ApprovedEvent!;
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO domain_events (
                id, promotion_id, type, occurred_at, actor_id, payload
            )
            VALUES ($1, $2, 'promotion_approved', $3, $4, '{}')
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(domainEvent.Id.Value);
            command.Parameters.AddWithValue(domainEvent.PromotionId.Value);
            command.Parameters.AddWithValue(domainEvent.OccurredAt);
            command.Parameters.AddWithValue(domainEvent.ActorId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO event_deliveries (event_id, consumer, available_at)
            VALUES
                ($1, 'audit', $2),
                ($1, 'release_notes', $2)
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(domainEvent.Id.Value);
            command.Parameters.AddWithValue(domainEvent.OccurredAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task StartDeployment(
        Promotion promotion,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = new NpgsqlCommand(
            """
            UPDATE promotions
            SET status = 'deploying'
            WHERE id = $1 AND status = 'approved'
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(promotion.Id.Value);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new InvalidPromotionTransition();
            }
        }

        var domainEvent = promotion.StartedEvent!;
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO domain_events (
                id, promotion_id, type, occurred_at, actor_id, payload
            )
            VALUES ($1, $2, 'deployment_started', $3, $4, '{}')
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(domainEvent.Id.Value);
            command.Parameters.AddWithValue(domainEvent.PromotionId.Value);
            command.Parameters.AddWithValue(domainEvent.OccurredAt);
            command.Parameters.AddWithValue(domainEvent.ActorId.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO event_deliveries (event_id, consumer, available_at)
            VALUES ($1, 'audit', $2)
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue(domainEvent.Id.Value);
            command.Parameters.AddWithValue(domainEvent.OccurredAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PromotionDetailsResponse?> Find(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var promotionCommand = new NpgsqlCommand(
            """
            SELECT p.application_id, p.application_version_id, av.label,
                   p.target_environment, p.status, p.requested_by,
                   p.requested_at, p.completed_at
            FROM promotions p
            JOIN application_versions av ON av.id = p.application_version_id
            WHERE p.id = $1
            """,
            connection);
        promotionCommand.Parameters.AddWithValue(id);
        await using var promotionReader =
            await promotionCommand.ExecuteReaderAsync(cancellationToken);
        if (!await promotionReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var applicationId = promotionReader.GetGuid(0);
        var applicationVersionId = promotionReader.GetGuid(1);
        var applicationVersionLabel = promotionReader.GetString(2);
        var targetEnvironment = promotionReader.GetString(3);
        var status = promotionReader.GetString(4);
        var requestedBy = promotionReader.GetGuid(5);
        var requestedAt = promotionReader.GetFieldValue<DateTimeOffset>(6);
        DateTimeOffset? completedAt = null;
        if (!promotionReader.IsDBNull(7))
        {
            completedAt = promotionReader.GetFieldValue<DateTimeOffset>(7);
        }
        await promotionReader.CloseAsync();

        var history = new List<PromotionHistoryItem>();
        await using var historyCommand = new NpgsqlCommand(
            """
            SELECT id, type, occurred_at, actor_id, payload
            FROM domain_events
            WHERE promotion_id = $1
            ORDER BY sequence
            """,
            connection);
        historyCommand.Parameters.AddWithValue(id);
        await using var historyReader =
            await historyCommand.ExecuteReaderAsync(cancellationToken);
        while (await historyReader.ReadAsync(cancellationToken))
        {
            history.Add(new PromotionHistoryItem(
                historyReader.GetGuid(0),
                historyReader.GetString(1),
                historyReader.GetFieldValue<DateTimeOffset>(2),
                historyReader.GetGuid(3),
                JsonSerializer.Deserialize<JsonElement>(historyReader.GetString(4))));
        }
        await historyReader.CloseAsync();

        ReleaseNotesDraftResponse? releaseNotesDraft = null;
        await using var notesCommand = new NpgsqlCommand(
            """
            SELECT id, content, breaking_changes, created_at
            FROM release_notes_drafts
            WHERE promotion_id = $1
            """,
            connection);
        notesCommand.Parameters.AddWithValue(id);
        await using var notesReader = await notesCommand.ExecuteReaderAsync(cancellationToken);
        if (await notesReader.ReadAsync(cancellationToken))
        {
            releaseNotesDraft = new ReleaseNotesDraftResponse(
                notesReader.GetGuid(0),
                notesReader.GetString(1),
                JsonSerializer.Deserialize<JsonElement>(notesReader.GetString(2)),
                notesReader.GetFieldValue<DateTimeOffset>(3));
        }

        return new PromotionDetailsResponse(
            id,
            applicationId,
            applicationVersionId,
            applicationVersionLabel,
            targetEnvironment,
            status,
            requestedBy,
            requestedAt,
            completedAt,
            history,
            releaseNotesDraft);
    }

}
