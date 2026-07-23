using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace ReleasePilot.IntegrationTests;

public sealed class PromotionListTests : PromotionIntegrationTest
{
    [Fact]
    public async Task ListsEmptyAndSinglePageHistory()
    {
        var emptyResponse = await Client.GetAsync(
            "/applications/01900000-0000-7000-8000-000000000102/promotions");

        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        var empty = await emptyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, empty.GetProperty("page").GetInt32());
        Assert.Equal(20, empty.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt64());
        Assert.Empty(empty.GetProperty("items").EnumerateArray());

        Client.DefaultRequestHeaders.Add(
            "X-User-Id",
            "01900000-0000-7000-8000-000000000002");
        var createdResponse = await Client.PostAsJsonAsync(
            "/promotions",
            new
            {
                applicationVersionId = "01900000-0000-7000-8000-000000000201",
                targetEnvironment = "dev"
            });
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();

        var response = await Client.GetAsync(
            "/applications/01900000-0000-7000-8000-000000000101/promotions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("totalCount").GetInt64());
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(
            created.GetProperty("id").GetString(),
            item.GetProperty("id").GetString());
        Assert.Equal("2026.7.1", item.GetProperty("applicationVersionLabel").GetString());
        Assert.Equal("requested", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task NavigatesDeterministicallyOrderedPages()
    {
        await using (var connection = new NpgsqlConnection(Database.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO promotions (
                    id, application_id, application_version_id, target_environment,
                    status, requested_by, requested_at, completed_at
                )
                VALUES
                    ('01900000-0000-7000-8000-000000000301',
                     '01900000-0000-7000-8000-000000000101',
                     '01900000-0000-7000-8000-000000000201', 'dev', 'completed',
                     '01900000-0000-7000-8000-000000000002',
                     '2026-07-24T10:00:00Z', '2026-07-24T10:01:00Z'),
                    ('01900000-0000-7000-8000-000000000302',
                     '01900000-0000-7000-8000-000000000101',
                     '01900000-0000-7000-8000-000000000202', 'dev', 'completed',
                     '01900000-0000-7000-8000-000000000002',
                     '2026-07-24T11:00:00Z', '2026-07-24T11:01:00Z'),
                    ('01900000-0000-7000-8000-000000000303',
                     '01900000-0000-7000-8000-000000000101',
                     '01900000-0000-7000-8000-000000000201', 'staging', 'completed',
                     '01900000-0000-7000-8000-000000000002',
                     '2026-07-24T11:00:00Z', '2026-07-24T11:01:00Z'),
                    ('01900000-0000-7000-8000-000000000304',
                     '01900000-0000-7000-8000-000000000101',
                     '01900000-0000-7000-8000-000000000202', 'staging', 'completed',
                     '01900000-0000-7000-8000-000000000002',
                     '2026-07-24T12:00:00Z', '2026-07-24T12:01:00Z'),
                    ('01900000-0000-7000-8000-000000000305',
                     '01900000-0000-7000-8000-000000000101',
                     '01900000-0000-7000-8000-000000000201', 'production', 'completed',
                     '01900000-0000-7000-8000-000000000002',
                     '2026-07-24T13:00:00Z', '2026-07-24T13:01:00Z')
                """,
                connection);
            await command.ExecuteNonQueryAsync();
        }

        var first = await (await Client.GetAsync(
                "/applications/01900000-0000-7000-8000-000000000101/promotions?page=1&pageSize=2"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await Client.GetAsync(
                "/applications/01900000-0000-7000-8000-000000000101/promotions?page=2&pageSize=2"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var third = await (await Client.GetAsync(
                "/applications/01900000-0000-7000-8000-000000000101/promotions?page=3&pageSize=2"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(5, first.GetProperty("totalCount").GetInt64());
        Assert.Equal(2, second.GetProperty("page").GetInt32());
        Assert.Equal(
            [
                "01900000-0000-7000-8000-000000000305",
                "01900000-0000-7000-8000-000000000304",
                "01900000-0000-7000-8000-000000000303",
                "01900000-0000-7000-8000-000000000302",
                "01900000-0000-7000-8000-000000000301"
            ],
            first.GetProperty("items").EnumerateArray()
                .Concat(second.GetProperty("items").EnumerateArray())
                .Concat(third.GetProperty("items").EnumerateArray())
                .Select(item => item.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task ValidatesApplicationAndPaginationBoundaries()
    {
        Assert.Equal(
            HttpStatusCode.OK,
            (await Client.GetAsync(
                "/applications/01900000-0000-7000-8000-000000000101/promotions?page=1&pageSize=100"))
            .StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await Client.GetAsync(
                "/applications/01900000-0000-7000-8000-000000000101/promotions?page=0"))
            .StatusCode);
        var invalidPageSize = await Client.GetAsync(
            "/applications/01900000-0000-7000-8000-000000000101/promotions?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageSize.StatusCode);
        var malformedProblem =
            await invalidPageSize.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "malformed_input",
            malformedProblem.GetProperty("code").GetString());
        Assert.False(
            string.IsNullOrWhiteSpace(
                malformedProblem.GetProperty("traceId").GetString()));
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await Client.GetAsync(
                "/applications/01900000-0000-7000-8000-000000000101/promotions?page=invalid"))
            .StatusCode);
        var missingApplication = await Client.GetAsync(
            "/applications/01900000-0000-7000-8000-999999999999/promotions");
        Assert.Equal(HttpStatusCode.NotFound, missingApplication.StatusCode);
        Assert.Equal(
            "resource_not_found",
            (await missingApplication.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code")
                .GetString());
    }
}
