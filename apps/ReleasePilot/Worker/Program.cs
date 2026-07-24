using ReleaseManagement.Application;
using ReleaseManagement.Domain;
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
builder.Services.AddSingleton<EventConsumer, AuditEventConsumer>();
builder.Services.AddSingleton<InMemoryNotificationPort>();
builder.Services.AddSingleton<INotificationPort>(
    services => services.GetRequiredService<InMemoryNotificationPort>());
builder.Services.AddSingleton<EventConsumer, NotificationEventConsumer>();
builder.Services.AddSingleton<IIssueTrackerPort, InMemoryIssueTrackerPort>();
builder.Services.AddSingleton<ILanguageModel, DeterministicLanguageModel>();
builder.Services.AddSingleton<IReleaseNotesDraftRepository>(
    services => new PostgreSqlReleaseNotesDraftRepository(
        connectionString,
        services.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<ReleaseNotesAgent>();
builder.Services.AddSingleton<EventConsumer, ReleaseNotesEventConsumer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
