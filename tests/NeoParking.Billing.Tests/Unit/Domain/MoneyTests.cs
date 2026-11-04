namespace NeoParking.Billing.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;

public class MoneyTests
{
    [Fact]
    public void Create_WithNegativeAmount_ShouldThrowDomainException()
    {
        var act = () => Money.Create(-1m);
        act.Should().Throw<DomainException>().WithMessage("*negative*");
    }

    [Fact]
    public void Create_ShouldRoundToTwoDecimals()
    {
        var money = Money.Create(10.996m);
        money.Value.Should().Be(11.00m);
    }

    [Fact]
    public void Zero_ShouldHaveAmountZero()
    {
        Money.Zero.Value.Should().Be(0m);
    }

    [Fact]
    public void Addition_ShouldSumAmounts()
    {
        var result = Money.Create(5m) + Money.Create(2.50m);
        result.Value.Should().Be(7.50m);
    }

    [Fact]
    public void Multiplication_ByHours_ShouldMultiplyAmount()
    {
        var result = Money.Create(4m) * 3;
        result.Value.Should().Be(12m);
    }

    [Fact]
    public void Equality_ShouldBeByValue()
    {
        Money.Create(10m).Should().Be(Money.Create(10m));
    }

    [Fact]
    public void Multiplication_ByNegativeFactor_ShouldThrowDomainException()
    {
        var act = () => Money.Create(10m) * -1;

        act.Should().Throw<DomainException>().WithMessage("*negative*");
    }
}
