using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace ReleasePilot.IntegrationTests;

public sealed class PromotionPersistenceTests : PromotionIntegrationTest
{
    [Fact]
    public async Task PersistsExactLifecycleEventsAndDeliveries()
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
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync($"/promotions/{promotionId}/approve", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await Client.PostAsync(
                $"/promotions/{promotionId}/start-deployment",
                null)).StatusCode);

        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT array_agg(e.type ORDER BY e.sequence),
                   array_agg(e.actor_id ORDER BY e.sequence),
                   bool_and(e.payload = CASE e.type
                       WHEN 'promotion_requested' THEN jsonb_build_object(
                           'applicationId', '01900000-0000-7000-8000-000000000101',
                           'applicationVersionId', '01900000-0000-7000-8000-000000000201',
                           'targetEnvironment', 'dev')
                       ELSE '{}'::jsonb
                   END),
                   array_agg(deliveries.consumers ORDER BY e.sequence),
                   bool_and(deliveries.available_at = e.occurred_at)
            FROM domain_events e
            CROSS JOIN LATERAL (
                SELECT string_agg(d.consumer, ',' ORDER BY d.consumer) AS consumers,
                       min(d.available_at) AS available_at
                FROM event_deliveries d
                WHERE d.event_id = e.id
            ) deliveries
            WHERE e.promotion_id = $1
            """,
            connection);
        command.Parameters.AddWithValue(Guid.Parse(promotionId!));
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(
            ["promotion_requested", "promotion_approved", "deployment_started"],
            reader.GetFieldValue<string[]>(0));
        Assert.Equal(
            Enumerable.Repeat(
                Guid.Parse("01900000-0000-7000-8000-000000000001"),
                3),
            reader.GetFieldValue<Guid[]>(1));
        Assert.True(reader.GetBoolean(2));
        Assert.Equal(
            ["audit", "audit,release_notes", "audit"],
            reader.GetFieldValue<string[]>(3));
        Assert.True(reader.GetBoolean(4));
    }
}
