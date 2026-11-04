namespace NeoParking.Api.Extensions;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using NeoParking.Shared.Kernel.Persistence;

public sealed class DatabaseHealthCheck(IEnumerable<IDatabaseProbe> probes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var probe in probes)
        {
            if (!await probe.CanConnectAsync(cancellationToken))
                return new HealthCheckResult(context.Registration.FailureStatus);
        }

        return HealthCheckResult.Healthy();
    }
}
