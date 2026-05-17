// HealthChecks/ReadinessCheck.cs
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Practice4.Services;

namespace Practice4.HealthChecks;

public class ReadinessCheck : IHealthCheck
{
    private readonly ProcessService _processService;

    public ReadinessCheck(ProcessService processService)
    {
        _processService = processService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_processService.IsDegraded)
            return Task.FromResult(HealthCheckResult.Unhealthy("Критическая деградация"));

        return Task.FromResult(HealthCheckResult.Healthy("Готов к работе"));
    }
}