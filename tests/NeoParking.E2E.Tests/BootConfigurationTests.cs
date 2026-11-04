namespace NeoParking.E2E.Tests;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// TD-108: o boot real fora de Development recusa um Jwt:Secret inválido antes de tocar no banco.
/// O caso válido é coberto por ApiFixture, que sobe a API no ambiente "Testing".
/// </summary>
public class BootConfigurationTests
{
    private const string UnreachableDb = "Server=127.0.0.1;Port=1;Database=none;Uid=none;";

    [Theory]
    [InlineData("CHANGE_ME_IN_PRODUCTION_MIN_32_CHARS_LONG_VALUE")]
    [InlineData("this-is-a-placeholder-value-that-is-long-enough")]
    [InlineData("short-secret-value")]
    [InlineData("")]
    [InlineData(null)]
    public void Boot_OutsideDevelopment_WithInvalidSecret_ShouldFailWithoutExposingTheValue(string? secret)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Access:DatabaseProvider", "MySQL");
            builder.UseSetting("Access:ConnectionString", UnreachableDb);
            builder.UseSetting("Management:ConnectionString", UnreachableDb);
            builder.UseSetting("Billing:ConnectionString", UnreachableDb);
            builder.UseSetting("MYSQL_PASSWORD", "boot-test-only");
            if (secret is not null)
                builder.UseSetting("Jwt:Secret", secret);
        });

        var act = () => factory.CreateClient();

        var details = act.Should().Throw<Exception>().Which.ToString();
        details.Should().Contain("Jwt:Secret");
        if (!string.IsNullOrEmpty(secret))
            details.Should().NotContain(secret);
    }

    [Fact]
    public void Boot_WithoutDatabasePassword_ShouldFailNamingTheVariable()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Access:DatabaseProvider", "MySQL");
            builder.UseSetting("Access:ConnectionString", UnreachableDb);
            builder.UseSetting("Management:ConnectionString", UnreachableDb);
            builder.UseSetting("Billing:ConnectionString", UnreachableDb);
            builder.UseSetting("MYSQL_PASSWORD", "");
            builder.UseSetting("Jwt:Secret", "0123456789abcdef0123456789abcdef");
        });

        var act = () => factory.CreateClient();

        act.Should().Throw<Exception>().Which.ToString().Should().Contain("MYSQL_PASSWORD");
    }

}
