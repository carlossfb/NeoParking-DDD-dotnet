namespace NeoParking.Management.Tests.Unit;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoParking.Management.Infrastructure;

public class ManagementModuleTests
{
    [Fact]
    public void AddManagementModule_ShouldNotStartAnOutboxProcessor()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Management:ConnectionString"] = "Server=localhost;Database=none;Uid=none;",
                ["MYSQL_PASSWORD"] = "unit-test-only",
            })
            .Build();

        var services = new ServiceCollection().AddManagementModule(configuration);

        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddManagementModule_WithoutItsConnectionString_ShouldFailInsteadOfUsingAccess()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Access:ConnectionString"] = "Server=localhost;Database=neoparking_access;Uid=none;",
                ["MYSQL_PASSWORD"] = "unit-test-only",
            })
            .Build();

        var act = () => new ServiceCollection().AddManagementModule(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Management:ConnectionString*");
    }
}
