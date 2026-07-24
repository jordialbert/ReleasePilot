using ReleaseManagement.Application;
using ReleaseManagement.Infrastructure;
using ReleasePilot.Worker;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration["ConnectionStrings:PostgreSQL"]!;

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEventDeliveryQueue>(
    services => new PostgreSqlEventDeliveryQueue(
        connectionString,
        services.GetRequiredService<TimeProvider>(),
        services.GetRequiredService<ILogger<PostgreSqlEventDeliveryQueue>>()));
builder.Services.AddSingleton<IAuditLogRepository>(
    new PostgreSqlAuditLogRepository(connectionString));
builder.Services.AddSingleton<AuditEventConsumer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
