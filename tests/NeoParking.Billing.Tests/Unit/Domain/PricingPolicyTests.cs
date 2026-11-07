namespace NeoParking.Billing.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Billing.Domain;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;

public class PricingPolicyTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static PricingPolicy MakePolicy(
        int minimumFeeCoverageMinutes = 60,
        int gracePeriodMinutes = 0,
        DateTime? effectiveFrom = null)
        => PricingPolicy.Define(
            OwnerId,
            Money.Create(10m),
            Money.Create(4m),
            minimumFeeCoverageMinutes,
            gracePeriodMinutes,
            effectiveFrom);

    [Fact]
    public void Define_WithValidData_ShouldBeActiveByDefault()
    {
        var policy = MakePolicy();

        policy.IsActive.Should().BeTrue();
        policy.EffectiveTo.Should().BeNull();
        policy.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Define_WithZeroCoverageMinutes_ShouldThrowDomainException()
    {
        var act = () => MakePolicy(minimumFeeCoverageMinutes: 0);
        act.Should().Throw<DomainException>().WithMessage("*coverage*");
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(10, 0)]
    [InlineData(0, 0)]
    public void Define_WithZeroFee_ShouldThrowDomainException(decimal minimumFee, decimal hourlyRate)
    {
        var act = () => PricingPolicy.Define(OwnerId, Money.Create(minimumFee), Money.Create(hourlyRate));
        act.Should().Throw<DomainException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void Define_WithNegativeGracePeriod_ShouldThrowDomainException()
    {
        var act = () => MakePolicy(gracePeriodMinutes: -1);
        act.Should().Throw<DomainException>().WithMessage("*Grace period*");
    }

    [Fact]
    public void Close_ShouldSetEffectiveToAndDeactivate()
    {
        var effectiveFrom = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var policy = MakePolicy(effectiveFrom: effectiveFrom);

        var closedAt = effectiveFrom.AddDays(30);
        policy.Close(closedAt);

        policy.IsActive.Should().BeFalse();
        policy.EffectiveTo.Should().Be(closedAt);
    }

    [Fact]
    public void Close_AlreadyClosed_ShouldThrowDomainException()
    {
        var effectiveFrom = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var policy = MakePolicy(effectiveFrom: effectiveFrom);
        policy.Close(effectiveFrom.AddDays(1));

        var act = () => policy.Close(effectiveFrom.AddDays(2));
        act.Should().Throw<DomainException>().WithMessage("*already closed*");
    }

    [Fact]
    public void Close_WithEffectiveToBeforeEffectiveFrom_ShouldThrowDomainException()
    {
        var effectiveFrom = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var policy = MakePolicy(effectiveFrom: effectiveFrom);

        var act = () => policy.Close(effectiveFrom.AddMinutes(-1));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void WasEffectiveAt_ShouldReturnTrueWithinOpenRange()
    {
        var effectiveFrom = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var policy = MakePolicy(effectiveFrom: effectiveFrom);

        policy.WasEffectiveAt(effectiveFrom.AddDays(10)).Should().BeTrue();
    }

    [Fact]
    public void WasEffectiveAt_ShouldReturnFalseAfterClosed()
    {
        var effectiveFrom = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var policy = MakePolicy(effectiveFrom: effectiveFrom);
        var closedAt = effectiveFrom.AddDays(30);
        policy.Close(closedAt);

        policy.WasEffectiveAt(closedAt.AddMinutes(1)).Should().BeFalse();
        policy.WasEffectiveAt(closedAt.AddMinutes(-1)).Should().BeTrue();
    }
}
