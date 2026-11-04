namespace NeoParking.E2E.Tests;

using FluentAssertions;
using NeoParking.Api.Extensions;

public class JwtSecretValidatorTests
{
    private const string Valid = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Validate_OutsideDevelopment_WithValidSecret_ShouldPass()
    {
        var act = () => JwtSecretValidator.Validate(Valid, isDevelopment: false);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_CountsBytesNotCharacters()
    {
        // 30 caracteres ASCII + "é" (dois bytes) = 31 caracteres, mas 32 bytes.
        var act = () => JwtSecretValidator.Validate(new string('a', 30) + "é", isDevelopment: false);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("change_me_with_a_long_enough_value_here")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_OutsideDevelopment_WithInvalidSecret_ShouldThrowWithoutTheValue(string? secret)
    {
        var act = () => JwtSecretValidator.Validate(secret, isDevelopment: false);

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().Contain("Jwt:Secret");
        if (!string.IsNullOrWhiteSpace(secret))
            message.Should().NotContain(secret);
    }

    [Theory]
    [InlineData("CHANGE_ME")]
    [InlineData("short")]
    public void Validate_InDevelopment_ShouldAcceptAnyConfiguredValue(string secret)
    {
        var act = () => JwtSecretValidator.Validate(secret, isDevelopment: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_InDevelopment_WithoutSecret_ShouldThrow()
    {
        var act = () => JwtSecretValidator.Validate(null, isDevelopment: true);

        act.Should().Throw<InvalidOperationException>();
    }
}
