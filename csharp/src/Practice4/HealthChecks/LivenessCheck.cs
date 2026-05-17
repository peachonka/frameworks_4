// HealthChecks/LivenessCheck.cs
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Practice4.HealthChecks;

public class LivenessCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Живой"));
    }
}