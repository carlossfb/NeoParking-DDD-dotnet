namespace NeoParking.Access.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Exceptions;

public class DocumentTests
{
    [Theory]
    [InlineData("11144477735", "11144477735")]
    [InlineData("111.444.777-35", "11144477735")]
    [InlineData(" 987.654.321-00 ", "98765432100")]
    public void Cpf_WhenValid_ShouldBeNormalized(string raw, string expected) =>
        Document.Create(ClientType.Individual, raw).Value.Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1234567890")]
    [InlineData("00000000000")]
    [InlineData("11144477734")]
    [InlineData("1114447773A")]
    [InlineData("11222333000181")]
    public void Cpf_WhenInvalid_ShouldBeRejected(string? raw) =>
        FluentActions.Invoking(() => Document.Create(ClientType.Individual, raw)).Should().Throw<DomainException>();

    // 12.ABC.345/01DE-35 é o exemplo publicado pela Receita Federal; 11.222.333/0001-81 é numérico.
    [Theory]
    [InlineData("12.ABC.345/01DE-35", "12ABC34501DE35")]
    [InlineData("12abc34501de35", "12ABC34501DE35")]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    public void Cnpj_WhenValid_ShouldBeNormalized(string raw, string expected) =>
        Document.Create(ClientType.Company, raw).Value.Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12ABC34501DE34")]
    [InlineData("12ABC34501DE3A")]
    [InlineData("12ABC34501DE3")]
    [InlineData("11222333000182")]
    [InlineData("00000000000000")]
    [InlineData("AAAAAAAAAAAAAA")]
    [InlineData("12@BC34501DE35")]
    [InlineData("11144477735")]
    public void Cnpj_WhenInvalid_ShouldBeRejected(string? raw) =>
        FluentActions.Invoking(() => Document.Create(ClientType.Company, raw)).Should().Throw<DomainException>();

    [Theory]
    [InlineData(ClientType.Individual, "11144477735", "***.444.777-**")]
    [InlineData(ClientType.Company, "12ABC34501DE35", "**.***.345/01DE-**")]
    public void Masked_ShouldHideTheEnds_AndToStringShouldNeverShowTheFullValue(ClientType type, string raw, string masked)
    {
        var document = Document.Create(type, raw);

        document.Masked.Should().Be(masked);
        document.ToString().Should().Be(masked).And.NotContain(raw);
    }

    [Theory]
    [InlineData(ClientType.Individual, "1114447773")]
    [InlineData(ClientType.Company, "12ABC34501DE34")]
    public void InvalidValue_ShouldNotBeEchoedInTheError(ClientType type, string raw) =>
        FluentActions.Invoking(() => Document.Create(type, raw)).Should().Throw<DomainException>()
            .Which.Message.Should().NotContain(raw);

    [Theory]
    [InlineData("Individual", ClientType.Individual)]
    [InlineData("company", ClientType.Company)]
    public void ClientType_ShouldBeParsedByName(string value, ClientType expected) =>
        ClientTypes.Parse(value).Should().Be(expected);

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("Foreigner")]
    [InlineData("")]
    [InlineData(null)]
    public void ClientType_WhenUnknown_ShouldBeRejected(string? value) =>
        FluentActions.Invoking(() => ClientTypes.Parse(value)).Should().Throw<DomainException>();
}
