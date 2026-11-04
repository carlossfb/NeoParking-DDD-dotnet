namespace NeoParking.Access.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Exceptions;

public class TicketCodeTests
{
    [Fact]
    public void Generate_ShouldUseTheDocumentedFormat()
    {
        TicketCode.Generate().Value.Should().MatchRegex("^[0-9A-F]{20}$");
    }

    [Fact]
    public void Generate_ShouldNotRepeat_NorBeSequential()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => TicketCode.Generate().Value).ToList();

        codes.Should().OnlyHaveUniqueItems();

        // Em um gerador sequencial, os prefixos de códigos consecutivos seriam iguais.
        codes.Select(c => c[..8]).Distinct().Should().HaveCountGreaterThan(990);
    }

    [Fact]
    public void Create_ShouldNormalizeWhatTheReaderSends()
    {
        TicketCode.Create(" 0123456789abcdef0123 ").Value.Should().Be("0123456789ABCDEF0123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123")]
    [InlineData("0123456789ABCDEF012G")]
    [InlineData("0123456789ABCDEF01234")]
    public void InvalidValues_ShouldBeRejected(string? raw)
    {
        TicketCode.TryParse(raw, out var code).Should().BeFalse();
        code.Should().BeNull();

        var act = () => TicketCode.Create(raw!);

        act.Should().Throw<DomainException>();
    }
}
