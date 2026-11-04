namespace NeoParking.Access.Tests.Unit.Infrastructure;

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NeoParking.Access.Domain;
using NeoParking.Access.Infrastructure;

public class AesGcmDocumentProtectorTests
{
    private static readonly string EncryptionKey = RandomNumberGenerator.GetHexString(64);
    private static readonly string HmacKey = RandomNumberGenerator.GetHexString(64);

    private static readonly Document Cpf = Document.Create(ClientType.Individual, "11144477735");
    private static readonly Document Cnpj = Document.Create(ClientType.Company, "12ABC34501DE35");

    private static DocumentProtectionKeys Keys(string? encryption, string? hmac) =>
        DocumentProtectionKeys.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DocumentProtectionKeys.EncryptionKeyName] = encryption,
                [DocumentProtectionKeys.HmacKeyName] = hmac,
            })
            .Build());

    private static AesGcmDocumentProtector Protector(string? encryption = null, string? hmac = null) =>
        new(Keys(encryption ?? EncryptionKey, hmac ?? HmacKey));

    [Fact]
    public void Protect_ShouldRoundTrip_ForBothTypes()
    {
        var protector = Protector();

        protector.Reveal(protector.Protect(Cpf)).Should().Be(Cpf);
        protector.Reveal(protector.Protect(Cnpj)).Should().Be(Cnpj);
    }

    [Fact]
    public void Protect_ShouldUseARandomNonce_AndKeepTheHmacStable()
    {
        var protector = Protector();

        var first = protector.Protect(Cpf);
        var second = protector.Protect(Cpf);

        first.Encrypted.Should().NotBe(second.Encrypted);
        first.Hmac.Should().Be(second.Hmac);
    }

    [Fact]
    public void Protect_ShouldCarryTheKeyVersion_AndNotContainTheValue()
    {
        var result = Protector().Protect(Cpf);

        result.Encrypted.Should().StartWith("v1.").And.NotContain(Cpf.Value);
        result.Hmac.Should().NotContain(Cpf.Value).And.HaveLength(64);
        Convert.ToBase64String(Encoding.UTF8.GetBytes(Cpf.Value)).Should().NotBeNull();
        result.Encrypted.Should().NotContain(Convert.ToBase64String(Encoding.UTF8.GetBytes(Cpf.Value)));
    }

    [Fact]
    public void Hmac_ShouldUseItsOwnKey_AndIncludeTheType()
    {
        var expected = HMACSHA256.HashData(Convert.FromHexString(HmacKey), Encoding.UTF8.GetBytes("Individual:11144477735"));

        Protector().Protect(Cpf).Hmac.Should().Be(Convert.ToHexString(expected).ToLowerInvariant());
    }

    [Fact]
    public void Hmac_ShouldChange_WithTheHmacKey()
    {
        var other = RandomNumberGenerator.GetHexString(64);

        Protector(hmac: other).Protect(Cpf).Hmac.Should().NotBe(Protector().Protect(Cpf).Hmac);
    }

    [Fact]
    public void Reveal_WithAnotherKey_ShouldFail_WithoutTheValue()
    {
        var encrypted = Protector().Protect(Cpf);
        var other = Protector(encryption: RandomNumberGenerator.GetHexString(64));

        FluentActions.Invoking(() => other.Reveal(encrypted)).Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(DocumentProtectionKeys.EncryptionKeyName).And.NotContain(Cpf.Value);
    }

    [Fact]
    public void Reveal_WhenTampered_ShouldFail()
    {
        var protector = Protector();
        var encrypted = protector.Protect(Cpf);
        var payload = Convert.FromBase64String(encrypted.Encrypted[3..]);
        payload[^1] ^= 1;

        var tampered = encrypted with { Encrypted = $"v1.{Convert.ToBase64String(payload)}" };

        FluentActions.Invoking(() => protector.Reveal(tampered)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reveal_WithTheWrongType_ShouldFail()
    {
        var protector = Protector();
        var encrypted = protector.Protect(Cpf);

        FluentActions.Invoking(() => protector.Reveal(encrypted with { Type = ClientType.Company }))
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("v2.AAAA")]
    [InlineData("no-version")]
    public void Reveal_WithAnUnknownKeyVersion_ShouldFail(string encrypted) =>
        FluentActions.Invoking(() => Protector().Reveal(new ProtectedDocument(ClientType.Individual, encrypted, "hmac")))
            .Should().Throw<InvalidOperationException>().WithMessage("*key version*");

    [Theory]
    [InlineData("v1.not-base64!")]
    [InlineData("v1.AAAA")]
    public void Reveal_WithAMalformedValue_ShouldFailClearly(string encrypted) =>
        FluentActions.Invoking(() => Protector().Reveal(new ProtectedDocument(ClientType.Individual, encrypted, "hmac")))
            .Should().Throw<InvalidOperationException>().WithMessage("*could not be decrypted*");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CHANGE_ME")]
    [InlineData("abcd")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Keys_WhenInvalid_ShouldNameTheVariable_WithoutTheValue(string? invalid)
    {
        var encryption = FluentActions.Invoking(() => Keys(invalid, HmacKey)).Should().Throw<InvalidOperationException>().Which.Message;
        var hmac = FluentActions.Invoking(() => Keys(EncryptionKey, invalid)).Should().Throw<InvalidOperationException>().Which.Message;

        encryption.Should().Contain(DocumentProtectionKeys.EncryptionKeyName);
        hmac.Should().Contain(DocumentProtectionKeys.HmacKeyName);
        if (!string.IsNullOrEmpty(invalid))
        {
            encryption.Should().NotContain(invalid);
            hmac.Should().NotContain(invalid);
        }
    }

    [Fact]
    public void Keys_WhenEqual_ShouldBeRefused() =>
        FluentActions.Invoking(() => Keys(EncryptionKey, EncryptionKey))
            .Should().Throw<InvalidOperationException>().WithMessage("*must be different*");
}
