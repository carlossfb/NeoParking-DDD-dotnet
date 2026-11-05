namespace NeoParking.Access.Tests.Unit.Infrastructure;

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Access.Infrastructure;

public class AesGcmDocumentProtectorTests
{
    private static readonly string EncryptionKey = RandomNumberGenerator.GetHexString(64);
    private static readonly string HmacKey = RandomNumberGenerator.GetHexString(64);

    private static readonly Document Cpf = Document.Create(ClientType.Individual, "11144477735");
    private static readonly Document Cnpj = Document.Create(ClientType.Company, "12ABC34501DE35");

    private static DocumentProtectionKeys Keys(string? encryption, string? hmac, string? version = null, string? previous = null) =>
        DocumentProtectionKeys.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DocumentProtectionKeys.EncryptionKeyName] = encryption,
                [DocumentProtectionKeys.HmacKeyName] = hmac,
                [DocumentProtectionKeys.KeyVersionName] = version,
                [DocumentProtectionKeys.PreviousKeysName] = previous,
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

    [Theory]
    [InlineData(ClientField.Name, "João da Silva")]
    [InlineData(ClientField.PhoneNumber, "+5511999999999")]
    public void Field_ShouldRoundTrip_WithARandomNonce_AndNotContainTheValue(ClientField field, string value)
    {
        var protector = Protector();

        var first = protector.ProtectField(field, value);

        first.Should().StartWith("v1.").And.NotContain(value);
        first.Should().NotBe(protector.ProtectField(field, value));
        protector.RevealField(field, first).Should().Be(value);
    }

    [Fact]
    public void Field_ShouldNotBeReadableAsAnotherField()
    {
        var protector = Protector();
        var name = protector.ProtectField(ClientField.Name, "Cliente");

        FluentActions.Invoking(() => protector.RevealField(ClientField.PhoneNumber, name))
            .Should().Throw<InvalidOperationException>().WithMessage("*could not be decrypted*");
    }

    [Fact]
    public void Field_WithTheLongestName_ShouldFitTheColumn()
    {
        var name = string.Concat(Enumerable.Repeat("\U0001F600"[..2], Client.NameMaxLength / 2));

        Protector().ProtectField(ClientField.Name, name).Length.Should().BeLessThanOrEqualTo(AccessDbContext.NameEncryptedMaxLength);
    }

    [Fact]
    public void Rotated_ShouldWriteTheCurrentVersion_AndReadThePreviousOne()
    {
        var oldProtector = Protector();
        var other = RandomNumberGenerator.GetHexString(64);
        var rotated = new AesGcmDocumentProtector(Keys(other, HmacKey, "2", $"1:{EncryptionKey}"));

        var oldDocument = oldProtector.Protect(Cpf);
        var oldName = oldProtector.ProtectField(ClientField.Name, "Cliente");

        rotated.Reveal(oldDocument).Should().Be(Cpf);
        rotated.RevealField(ClientField.Name, oldName).Should().Be("Cliente");
        rotated.Protect(Cpf).Encrypted.Should().StartWith("v2.");
        rotated.ProtectField(ClientField.Name, "Cliente").Should().StartWith("v2.");
        rotated.CurrentKeyVersion.Should().Be("v2");
    }

    [Fact]
    public void RotateDocument_ShouldChangeTheVersion_AndKeepTheStoredHmac()
    {
        var oldDocument = Protector().Protect(Cpf);
        var rotated = new AesGcmDocumentProtector(Keys(RandomNumberGenerator.GetHexString(64), HmacKey, "2", $"1:{EncryptionKey}"));

        var result = rotated.RotateDocument(oldDocument);

        result.Encrypted.Should().StartWith("v2.");
        result.Hmac.Should().Be(oldDocument.Hmac);
        rotated.Reveal(result).Should().Be(Cpf);
    }

    [Fact]
    public void Reveal_WithAMissingPreviousKey_ShouldNameTheVersionAndTheVariable_WithoutTheValue()
    {
        var oldDocument = Protector().Protect(Cpf);
        var rotated = new AesGcmDocumentProtector(Keys(RandomNumberGenerator.GetHexString(64), HmacKey, "2"));

        var message = FluentActions.Invoking(() => rotated.Reveal(oldDocument)).Should().Throw<InvalidOperationException>().Which.Message;

        message.Should().Contain("v1").And.Contain(DocumentProtectionKeys.PreviousKeysName).And.NotContain(Cpf.Value);
    }

    [Theory]
    [InlineData("0", null)]
    [InlineData("abc", null)]
    [InlineData("2", "1")]
    [InlineData("2", "x:abcd")]
    [InlineData("2", "1:abcd")]
    public void Keys_WithAnInvalidRotationSetting_ShouldBeRefused_WithoutTheValue(string version, string? previous)
    {
        var message = FluentActions.Invoking(() => Keys(EncryptionKey, HmacKey, version, previous))
            .Should().Throw<InvalidOperationException>().Which.Message;

        message.Should().Match(m => m.Contains(DocumentProtectionKeys.KeyVersionName) || m.Contains(DocumentProtectionKeys.PreviousKeysName));
        message.Should().NotContain("abcd");
    }

    [Fact]
    public void Keys_WhenAPreviousVersionIsTheCurrentOne_ShouldBeRefused() =>
        FluentActions.Invoking(() => Keys(EncryptionKey, HmacKey, "2", $"2:{RandomNumberGenerator.GetHexString(64)}"))
            .Should().Throw<InvalidOperationException>().WithMessage("*current version*");

    [Fact]
    public void Keys_WhenAPreviousKeyRepeatsTheCurrentOrTheHmacKey_ShouldBeRefused()
    {
        FluentActions.Invoking(() => Keys(EncryptionKey, HmacKey, "2", $"1:{EncryptionKey}"))
            .Should().Throw<InvalidOperationException>().WithMessage("*must be different*");
        FluentActions.Invoking(() => Keys(EncryptionKey, HmacKey, "2", $"1:{HmacKey}"))
            .Should().Throw<InvalidOperationException>().WithMessage("*must be different*");
    }

    [Fact]
    public void Keys_WhenEqual_ShouldBeRefused() =>
        FluentActions.Invoking(() => Keys(EncryptionKey, EncryptionKey))
            .Should().Throw<InvalidOperationException>().WithMessage("*must be different*");
}
