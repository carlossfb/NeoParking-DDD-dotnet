using FluentAssertions;
using main.domain.vo;
using main.domain.exception;

namespace Access.Tests.Unit.Domain;

public class CpfTests
{
    [Theory]
    [InlineData("11144477735")]
    [InlineData("98765432100")]
    public void Create_WithValidCpf_ShouldCreateCpf(string validCpf)
    {
        var cpf = Cpf.Create(validCpf);

        cpf.Should().NotBeNull();
        cpf.Document.Should().Be(validCpf);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("12345678901234")]
    public void Create_WithInvalidCpf_ShouldThrowException(string invalidCpf)
    {
        var act = () => Cpf.Create(invalidCpf);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ToString_ShouldReturnFormattedCpf()
    {
        var cpf = Cpf.Create("11144477735");

        var result = cpf.ToString();

        result.Should().Be("111.444.777-35");
    }
}