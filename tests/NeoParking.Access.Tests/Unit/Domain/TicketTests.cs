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
}
