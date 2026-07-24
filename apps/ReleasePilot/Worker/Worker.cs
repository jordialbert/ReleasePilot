using ReleaseManagement.Application;

namespace ReleasePilot.Worker;

public sealed class Worker(
    AuditEventConsumer audit,
    TimeProvider timeProvider,
    ILogger<Worker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ReleasePilot worker started");

        // Infrastructure failures stop the host so Docker can restart the process.
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await audit.ProcessNext(stoppingToken))
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(500),
                        timeProvider,
                        stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("ReleasePilot worker stopping");
        }
    }
}
