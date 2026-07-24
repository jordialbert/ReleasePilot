using Microsoft.Extensions.Options;
using ReleaseManagement.Application;

namespace ReleasePilot.Worker;

public sealed class Worker(
    IEnumerable<EventConsumer> consumers,
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
                var processedAny = false;
                foreach (var consumer in consumers)
                {
                    processedAny |= await consumer.ProcessNext(
                        stoppingToken,
                        processingCancellation.Token);
                }

                if (!processedAny)
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
