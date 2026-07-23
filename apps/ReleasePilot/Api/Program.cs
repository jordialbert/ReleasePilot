using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using ReleaseManagement.Application;
using ReleaseManagement.Domain;
using ReleaseManagement.Infrastructure;
using ReleasePilot.Api.Shared;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration["ConnectionStrings:PostgreSQL"]!;

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The request is malformed."
            };
            problem.Extensions["code"] = "malformed_input";
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            return new BadRequestObjectResult(problem);
        };
    });
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(new PostgreSqlPromotionRepository(connectionString));
builder.Services.AddSingleton<IPromotionRepository>(
    services => services.GetRequiredService<PostgreSqlPromotionRepository>());
builder.Services.AddSingleton<IPromotionDetailsReader>(
    services => services.GetRequiredService<PostgreSqlPromotionRepository>());
builder.Services.AddSingleton<IPromotionListReader>(
    new PostgreSqlPromotionListReader(connectionString));
builder.Services.AddSingleton<IUserRepository>(
    new PostgreSqlUserRepository(connectionString));
builder.Services.AddSingleton<IApplicationRepository>(
    new PostgreSqlApplicationRepository(connectionString));
builder.Services.AddSingleton<IApplicationVersionRepository>(
    new PostgreSqlApplicationVersionRepository(connectionString));
builder.Services.AddSingleton<InMemoryDeploymentPort>();
builder.Services.AddSingleton<IDeploymentPort>(
    services => services.GetRequiredService<InMemoryDeploymentPort>());
builder.Services.AddScoped<RequestPromotionCommandHandler>();
builder.Services.AddScoped<ApprovePromotionCommandHandler>();
builder.Services.AddScoped<StartDeploymentCommandHandler>();
builder.Services.AddScoped<GetPromotionDetailsQueryHandler>();
builder.Services.AddScoped<ListPromotionsQueryHandler>();
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

app.UseExceptionHandler();
app.UseSwagger(options => options.RouteTemplate = "docs/{documentName}/swagger.json");
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "docs";
    options.SwaggerEndpoint("/docs/v1/swagger.json", "ReleasePilot v1");
});
app.MapControllers();
app.Run();

public partial class Program;
