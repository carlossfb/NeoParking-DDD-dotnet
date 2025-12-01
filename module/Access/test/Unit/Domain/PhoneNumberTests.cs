using FluentAssertions;
using main.domain.vo;
using main.domain.exception;

namespace Access.Tests.Unit.Domain;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("+5511999999999")]
    [InlineData("+5521987654321")]
    public void Create_WithValidPhoneNumber_ShouldCreatePhoneNumber(string validPhone)
    {
        var phoneNumber = PhoneNumber.Create(validPhone);

        phoneNumber.Should().NotBeNull();
        phoneNumber.Value.Should().Be(validPhone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("invalid")]
    public void Create_WithInvalidPhoneNumber_ShouldThrowException(string invalidPhone)
    {
        var act = () => PhoneNumber.Create(invalidPhone);
        act.Should().Throw<DomainException>();
    }
}