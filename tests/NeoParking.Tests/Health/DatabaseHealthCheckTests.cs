namespace NeoParking.Tests.Health;

using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NeoParking.Api.Extensions;
using NeoParking.Shared.Kernel.Persistence;

public class DatabaseHealthCheckTests
{
    private sealed class Probe(bool canConnect) : IDatabaseProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Task.FromResult(canConnect);
    }

    private static Task<HealthCheckResult> CheckAsync(params bool[] databases)
    {
        var check = new DatabaseHealthCheck(databases.Select(canConnect => new Probe(canConnect)));
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", check, HealthStatus.Unhealthy, null),
        };

        return check.CheckHealthAsync(context);
    }

    [Fact]
    public async Task AllDatabasesUp_ShouldBeHealthy() =>
        (await CheckAsync(true, true, true)).Status.Should().Be(HealthStatus.Healthy);

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task AnyDatabaseDown_ShouldBeUnhealthy(bool access, bool management, bool billing) =>
        (await CheckAsync(access, management, billing)).Status.Should().Be(HealthStatus.Unhealthy);
}
