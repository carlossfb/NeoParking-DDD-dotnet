namespace NeoParking.Billing.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Billing.Domain;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;

public class TicketTests
{
    private static readonly DateTime EntryAt = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    private static PricingPolicy MakePolicy()
        => PricingPolicy.Define(
            Guid.NewGuid(), Money.Create(10m), Money.Create(4m),
            minimumFeeCoverageMinutes: 60, gracePeriodMinutes: 0, effectiveFrom: EntryAt.AddDays(-1));

    [Fact]
    public void OpenFromAccess_ShouldStartAsOpenWithoutFee()
    {
        var ticket = Ticket.OpenFromAccess(Guid.NewGuid(), "ABC1234", EntryAt);

        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.ExitAt.Should().BeNull();
        ticket.ChargedFee.Should().BeNull();
    }

    [Fact]
    public void OpenFromAccess_ShouldNormalizePlate()
    {
        var ticket = Ticket.OpenFromAccess(Guid.NewGuid(), " abc-1234 ", EntryAt);
        ticket.Plate.Should().Be("ABC1234");
    }

    [Fact]
    public void OpenFromAccess_WithEmptyPlate_ShouldThrowDomainException()
    {
        var act = () => Ticket.OpenFromAccess(Guid.NewGuid(), "", EntryAt);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Close_ShouldCalculateAndFreezeFee()
    {
        var ticket = Ticket.OpenFromAccess(Guid.NewGuid(), "ABC1234", EntryAt);
        var policy = MakePolicy();
        var exitAt = EntryAt.AddMinutes(90);

        ticket.Close(exitAt, policy);

        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ExitAt.Should().Be(exitAt);
        ticket.ChargedFee.Should().NotBeNull();
        ticket.ChargedFee!.Amount.Should().Be(Money.Create(14m)); // 10 + 4*1 (30min excedentes arredonda p/ 1h)
    }

    [Fact]
    public void Close_AlreadyClosed_ShouldThrowDomainException()
    {
        var ticket = Ticket.OpenFromAccess(Guid.NewGuid(), "ABC1234", EntryAt);
        var policy = MakePolicy();
        ticket.Close(EntryAt.AddHours(1), policy);

        var act = () => ticket.Close(EntryAt.AddHours(2), policy);
        act.Should().Throw<DomainException>().WithMessage("*already closed*");
    }

    [Fact]
    public void Close_WithExitBeforeEntry_ShouldThrowDomainException()
    {
        var ticket = Ticket.OpenFromAccess(Guid.NewGuid(), "ABC1234", EntryAt);
        var policy = MakePolicy();

        var act = () => ticket.Close(EntryAt.AddMinutes(-1), policy);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Close_FutureChangesInPolicy_ShouldNotAffectAlreadyChargedFee()
    {
        var ticket = Ticket.OpenFromAccess(Guid.NewGuid(), "ABC1234", EntryAt);
        var originalPolicy = MakePolicy();
        ticket.Close(EntryAt.AddMinutes(90), originalPolicy);

        var chargedAmount = ticket.ChargedFee!.Amount;

        // Mesmo que uma nova política seja definida depois, a Fee já congelada não muda.
        var newPolicy = PricingPolicy.Define(
            Guid.NewGuid(), Money.Create(999m), Money.Create(999m), 60, 0, DateTime.UtcNow);

        ticket.ChargedFee.Amount.Should().Be(chargedAmount);
        ticket.ChargedFee.PricingPolicyId.Should().Be(originalPolicy.Id);
        ticket.ChargedFee.PricingPolicyId.Should().NotBe(newPolicy.Id);
    }
}
