using Microsoft.Extensions.Options;
using ReleaseManagement.Application;

namespace ReleasePilot.Worker;

public sealed class Worker(
    AuditEventConsumer audit,
    TimeProvider timeProvider,
    IOptions<HostOptions> hostOptions,
    ILogger<Worker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ReleasePilot worker started");

        using var processingCancellation = new CancellationTokenSource();
        using var stoppingRegistration = stoppingToken.Register(
            () => processingCancellation.CancelAfter(
                hostOptions.Value.ShutdownTimeout - TimeSpan.FromSeconds(1)));

        // Infrastructure failures stop the host so Docker can restart the process.
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await audit.ProcessNext(
                        stoppingToken,
                        processingCancellation.Token))
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(500),
                        timeProvider,
                        stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("ReleasePilot worker stopping");
        }
    }
}
