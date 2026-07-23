using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ReleasePilot.IntegrationTests;

public abstract class PromotionIntegrationTest : IAsyncLifetime
{
    protected PostgreSqlContainer Database { get; } = new PostgreSqlBuilder("postgres:18.4")
        .Build();
    protected WebApplicationFactory<Program> Application { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Database.StartAsync();
        await using var connection = new NpgsqlConnection(Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "001-schema.sql"))
            + await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "002-seed.sql")),
            connection);
        await command.ExecuteNonQueryAsync();

        Application = new WebApplicationFactory<Program>().WithWebHostBuilder(
            builder => builder.UseSetting(
                "ConnectionStrings:PostgreSQL",
                Database.GetConnectionString()));
        Client = Application.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Application.DisposeAsync();
        await Database.DisposeAsync();
    }
}
