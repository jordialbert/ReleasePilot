using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration["ConnectionStrings:PostgreSQL"]!;

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddHealthChecks()
    .AddCheck("postgresql", () =>
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = new NpgsqlCommand(
                """
                SELECT to_regclass('public.users') IS NOT NULL
                   AND to_regclass('public.applications') IS NOT NULL
                   AND to_regclass('public.application_versions') IS NOT NULL
                """,
                connection);

            var initialized = (bool)command.ExecuteScalar()!;
            if (initialized)
            {
                return HealthCheckResult.Healthy();
            }

            return HealthCheckResult.Unhealthy("PostgreSQL schema is not initialized.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.", exception);
        }
    });

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/swagger"));
app.Run();

public partial class Program;
