namespace NeoParking.E2E.Tests;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NeoParking.Access.Infrastructure;
using System.Security.Cryptography;

/// <summary>
/// TD-108: o boot real fora de Development recusa um Jwt:Secret inválido antes de tocar no banco.
/// O caso válido é coberto por ApiFixture, que sobe a API no ambiente "Testing".
/// </summary>
public class BootConfigurationTests
{
    private const string UnreachableDb = "Server=127.0.0.1;Port=1;Database=none;Uid=none;";

    private static void WithValidDocumentKeys(IWebHostBuilder builder)
    {
        builder.UseSetting(DocumentProtectionKeys.EncryptionKeyName, RandomNumberGenerator.GetHexString(64));
        builder.UseSetting(DocumentProtectionKeys.HmacKeyName, RandomNumberGenerator.GetHexString(64));
    }

    [Theory]
    [InlineData(DocumentProtectionKeys.EncryptionKeyName, null)]
    [InlineData(DocumentProtectionKeys.HmacKeyName, null)]
    [InlineData(DocumentProtectionKeys.EncryptionKeyName, "CHANGE_ME")]
    [InlineData(DocumentProtectionKeys.HmacKeyName, "abcd1234")]
    public void Boot_WithAnInvalidDocumentKey_ShouldFailNamingTheKey_WithoutTheValue(string key, string? value)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Access:ConnectionString", UnreachableDb);
            builder.UseSetting("Management:ConnectionString", UnreachableDb);
            builder.UseSetting("Billing:ConnectionString", UnreachableDb);
            builder.UseSetting("MYSQL_PASSWORD", "boot-test-only");
            builder.UseSetting("Jwt:Secret", Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
            WithValidDocumentKeys(builder);
            builder.UseSetting(key, value);
        });

        var details = FluentActions.Invoking(() => factory.CreateClient()).Should().Throw<Exception>().Which.ToString();

        details.Should().Contain(key);
        if (value is not null)
            details.Should().NotContain(value);
    }

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
            builder.UseSetting("Access:ConnectionString", UnreachableDb);
            WithValidDocumentKeys(builder);
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
            builder.UseSetting("Access:ConnectionString", UnreachableDb);
            WithValidDocumentKeys(builder);
            builder.UseSetting("Management:ConnectionString", UnreachableDb);
            builder.UseSetting("Billing:ConnectionString", UnreachableDb);
            builder.UseSetting("MYSQL_PASSWORD", "");
            builder.UseSetting("Jwt:Secret", "0123456789abcdef0123456789abcdef");
        });

        var act = () => factory.CreateClient();

        act.Should().Throw<Exception>().Which.ToString().Should().Contain("MYSQL_PASSWORD");
    }

    [Fact]
    public void Boot_WithoutBillingConnectionString_ShouldFailNamingTheKey()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Access:ConnectionString", UnreachableDb);
            WithValidDocumentKeys(builder);
            builder.UseSetting("Management:ConnectionString", UnreachableDb);
            builder.UseSetting("MYSQL_PASSWORD", "boot-test-only");
            builder.UseSetting("Jwt:Secret", "0123456789abcdef0123456789abcdef");
        });

        var act = () => factory.CreateClient();

        act.Should().Throw<Exception>().Which.ToString().Should().Contain("Billing:ConnectionString");
    }
}
