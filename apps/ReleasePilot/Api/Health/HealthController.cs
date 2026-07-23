using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ReleasePilot.Api.Health;

[ApiController]
[Route("health")]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var result = Content(report.Status.ToString(), "text/plain");
        if (report.Status == HealthStatus.Unhealthy)
        {
            result.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }

        return result;
    }
}
