using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace ReleasePilot.IntegrationTests;

public sealed class ApprovePromotionTests : PromotionIntegrationTest
{
    [Fact]
    public async Task ApprovesRequestedPromotionWithControlledErrors()
    {
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");
        var created = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "dev"
            });
        var promotion = await created.Content.ReadFromJsonAsync<JsonElement>();
        var promotionId = promotion.GetProperty("id").GetString();

        var approval = await Client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);

        Assert.Equal(HttpStatusCode.NoContent, approval.StatusCode);
        var details = await Client.GetFromJsonAsync<JsonElement>(
            $"/promotions/{promotionId}");
        Assert.Equal("approved", details.GetProperty("status").GetString());
        Assert.Equal(2, details.GetProperty("history").GetArrayLength());
        Assert.Equal(
            "promotion_requested",
            details.GetProperty("history")[0].GetProperty("type").GetString());
        Assert.Equal(
            "promotion_approved",
            details.GetProperty("history")[1].GetProperty("type").GetString());

        var invalidTransition = await Client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);
        Assert.Equal(HttpStatusCode.Conflict, invalidTransition.StatusCode);
        Assert.Equal(
            "invalid_promotion_transition",
            (await invalidTransition.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());

        Client.DefaultRequestHeaders.Remove("X-User-Id");
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000002");

        var forbidden = await Client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(
            "only_approver_can_approve",
            (await forbidden.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
    }

    [Fact]
    public async Task RollsBackPromotionSnapshotEventAndDeliveriesTogether()
    {
        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000001");
        var created = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "dev"
            });
        var promotion = await created.Content.ReadFromJsonAsync<JsonElement>();
        var promotionId = promotion.GetProperty("id").GetString();

        await using (var connection = new NpgsqlConnection(Database.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                CREATE FUNCTION reject_release_notes_delivery()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF NEW.consumer = 'release_notes' THEN
                        RAISE EXCEPTION 'Rejected release notes delivery.';
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER reject_release_notes_delivery
                BEFORE INSERT ON event_deliveries
                FOR EACH ROW
                EXECUTE FUNCTION reject_release_notes_delivery()
                """,
                connection);
            await command.ExecuteNonQueryAsync();
        }

        var approval = await Client.PostAsync(
            $"/promotions/{promotionId}/approve",
            null);

        Assert.Equal(HttpStatusCode.InternalServerError, approval.StatusCode);
        await using var verification = new NpgsqlConnection(Database.GetConnectionString());
        await verification.OpenAsync();
        await using var verificationCommand = new NpgsqlCommand(
            """
            SELECT p.status,
                   (SELECT array_agg(type ORDER BY sequence)
                    FROM domain_events
                    WHERE promotion_id = p.id),
                   (SELECT array_agg(d.consumer ORDER BY d.consumer)
                    FROM event_deliveries d
                    JOIN domain_events e ON e.id = d.event_id
                    WHERE e.promotion_id = p.id)
            FROM promotions p
            WHERE p.id = $1
            """,
            verification);
        verificationCommand.Parameters.AddWithValue(Guid.Parse(promotionId!));
        await using var reader = await verificationCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("requested", reader.GetString(0));
        Assert.Equal(["promotion_requested"], reader.GetFieldValue<string[]>(1));
        Assert.Equal(["audit"], reader.GetFieldValue<string[]>(2));
    }
}
