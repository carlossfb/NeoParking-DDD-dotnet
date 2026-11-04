// Unit/Domain/TicketTests.cs
namespace NeoParking.Access.Tests.Unit.Domain;

using FluentAssertions;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Exceptions;

public class TicketTests
{
    private static readonly DateTime EntryAt = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RegisterEntry_ShouldStartAsOpenWithoutExit()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);

        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.ExitAt.Should().BeNull();
        ticket.EntryAt.Should().Be(EntryAt);
    }

    [Fact]
    public void RegisterEntry_WithClientId_ShouldAssociateClient()
    {
        var clientId = Guid.NewGuid();
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), clientId, EntryAt);

        ticket.ClientId.Should().Be(clientId);
    }

    [Fact]
    public void RegisterEntry_WithoutClientId_ShouldAllowWalkIn()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);

        ticket.ClientId.Should().BeNull();
    }

    [Fact]
    public void RegisterExit_ShouldCloseTicket()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);
        var exitAt = EntryAt.AddHours(1);

        ticket.RegisterExit(exitAt);

        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ExitAt.Should().Be(exitAt);
    }

    [Fact]
    public void RegisterExit_AlreadyClosed_ShouldThrowDomainException()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);
        ticket.RegisterExit(EntryAt.AddHours(1));

        var act = () => ticket.RegisterExit(EntryAt.AddHours(2));
        act.Should().Throw<DomainException>().WithMessage("*already closed*");
    }

    [Fact]
    public void RegisterExit_WithExitBeforeEntry_ShouldThrowDomainException()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);

        var act = () => ticket.RegisterExit(EntryAt.AddMinutes(-1));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RegisterEntry_ShouldAssignACode()
    {
        var first = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);
        var second = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);

        first.Code.Should().NotBeNull();
        first.Code.Should().NotBe(second.Code);
    }

    [Theory]
    [InlineData(ExitMethod.TicketId)]
    [InlineData(ExitMethod.Code)]
    public void RegisterExit_ShouldRecordTheMethod(ExitMethod method)
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);

        ticket.RegisterExit(EntryAt.AddHours(1), method);

        ticket.ExitMethod.Should().Be(method);
        ticket.ExitReason.Should().BeNull();
        ticket.ExitOperatorId.Should().BeNull();
    }

    [Fact]
    public void RegisterLostTicketExit_ShouldRecordMethodReasonAndAuthor()
    {
        var operatorId = Guid.NewGuid();
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);

        ticket.RegisterLostTicketExit(operatorId, "  Ticket perdido  ", EntryAt.AddHours(1));

        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.EntryAt.Should().Be(EntryAt);
        ticket.ExitMethod.Should().Be(ExitMethod.Plate);
        ticket.ExitReason.Should().Be("Ticket perdido");
        ticket.ExitOperatorId.Should().Be(operatorId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterLostTicketExit_WithoutReason_ShouldBeRejectedAndKeepTheTicketOpen(string? reason)
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);

        var act = () => ticket.RegisterLostTicketExit(Guid.NewGuid(), reason!, EntryAt.AddHours(1));

        act.Should().Throw<DomainException>();
        ticket.Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public void RegisterLostTicketExit_WithTooLongReason_ShouldBeRejected()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);

        var act = () => ticket.RegisterLostTicketExit(
            Guid.NewGuid(), new string('x', Ticket.ExitReasonMaxLength + 1), EntryAt.AddHours(1));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RegisterLostTicketExit_WithoutAuthor_ShouldBeRejected()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);

        var act = () => ticket.RegisterLostTicketExit(Guid.Empty, "Ticket perdido", EntryAt.AddHours(1));

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RegisterLostTicketExit_OnAClosedTicket_ShouldKeepTheOriginalExit()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: EntryAt);
        ticket.RegisterExit(EntryAt.AddHours(1), ExitMethod.Code);

        var act = () => ticket.RegisterLostTicketExit(Guid.NewGuid(), "Ticket perdido", EntryAt.AddHours(2));

        act.Should().Throw<DomainException>();
        ticket.ExitMethod.Should().Be(ExitMethod.Code);
        ticket.ExitReason.Should().BeNull();
    }
}
