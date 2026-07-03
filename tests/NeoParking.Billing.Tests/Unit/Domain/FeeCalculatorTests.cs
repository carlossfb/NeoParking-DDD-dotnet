namespace NeoParking.Billing.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Billing.Domain;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;

public class FeeCalculatorTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime EntryAt = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    private static PricingPolicy MakePolicy(
        decimal minimumFee = 10m,
        decimal hourlyRate = 4m,
        int minimumFeeCoverageMinutes = 60,
        int gracePeriodMinutes = 0)
        => PricingPolicy.Define(
            OwnerId,
            Money.Create(minimumFee),
            Money.Create(hourlyRate),
            minimumFeeCoverageMinutes,
            gracePeriodMinutes,
            EntryAt.AddDays(-1));

    [Fact]
    public void Calculate_WithinGracePeriod_ShouldBeFree()
    {
        var policy = MakePolicy(gracePeriodMinutes: 15);
        var exitAt = EntryAt.AddMinutes(10);

        var fee = FeeCalculator.Calculate(policy, EntryAt, exitAt);

        fee.Amount.Should().Be(Money.Zero);
        fee.BilledHours.Should().Be(0);
    }

    [Fact]
    public void Calculate_ExactlyAtGracePeriodBoundary_ShouldBeFree()
    {
        var policy = MakePolicy(gracePeriodMinutes: 15);
        var exitAt = EntryAt.AddMinutes(15);

        var fee = FeeCalculator.Calculate(policy, EntryAt, exitAt);

        fee.Amount.Should().Be(Money.Zero);
    }

    [Fact]
    public void Calculate_WithinMinimumFeeCoverage_ShouldChargeOnlyMinimumFee()
    {
        var policy = MakePolicy(minimumFee: 10m, hourlyRate: 4m, minimumFeeCoverageMinutes: 60);
        var exitAt = EntryAt.AddMinutes(45);

        var fee = FeeCalculator.Calculate(policy, EntryAt, exitAt);

        fee.Amount.Should().Be(Money.Create(10m));
        fee.BilledHours.Should().Be(0);
    }

    [Fact]
    public void Calculate_ExceedingCoverageByOneMinute_ShouldRoundUpToOneExtraHour()
    {
        var policy = MakePolicy(minimumFee: 10m, hourlyRate: 4m, minimumFeeCoverageMinutes: 60);
        var exitAt = EntryAt.AddMinutes(61);

        var fee = FeeCalculator.Calculate(policy, EntryAt, exitAt);

        fee.BilledHours.Should().Be(1);
        fee.Amount.Should().Be(Money.Create(14m)); // 10 + 4*1
    }

    [Fact]
    public void Calculate_ExceedingCoverageByTwoHoursAndAFraction_ShouldChargeThreeExtraHours()
    {
        var policy = MakePolicy(minimumFee: 10m, hourlyRate: 4m, minimumFeeCoverageMinutes: 60);
        var exitAt = EntryAt.AddMinutes(60 + 120 + 5); // 1h de cobertura + 2h05min excedentes

        var fee = FeeCalculator.Calculate(policy, EntryAt, exitAt);

        fee.BilledHours.Should().Be(3); // ceil(125/60) = 3
        fee.Amount.Should().Be(Money.Create(22m)); // 10 + 4*3
    }

    [Fact]
    public void Calculate_ShouldSnapshotTheSourcePolicyValues()
    {
        var policy = MakePolicy(minimumFee: 15m, hourlyRate: 5m);
        var exitAt = EntryAt.AddHours(3);

        var fee = FeeCalculator.Calculate(policy, EntryAt, exitAt);

        fee.PricingPolicyId.Should().Be(policy.Id);
        fee.MinimumFee.Should().Be(Money.Create(15m));
        fee.HourlyRate.Should().Be(Money.Create(5m));
    }

    [Fact]
    public void Calculate_WithExitBeforeOrEqualToEntry_ShouldThrowDomainException()
    {
        var policy = MakePolicy();

        var act = () => FeeCalculator.Calculate(policy, EntryAt, EntryAt);
        act.Should().Throw<DomainException>().WithMessage("*Exit time*");
    }
}
