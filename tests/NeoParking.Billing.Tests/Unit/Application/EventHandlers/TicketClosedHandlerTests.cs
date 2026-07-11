namespace NeoParking.Billing.Tests.Unit.Application.EventHandlers;

using FluentAssertions;
using Moq;
using NeoParking.Billing.Application;
using NeoParking.Billing.Application.EventHandlers;
using NeoParking.Billing.Domain;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;

public class TicketClosedHandlerTests
{
    private readonly Mock<ITicketRepository> _tickets = new();
    private readonly Mock<IPricingPolicyRepository> _policies = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly TicketClosedHandler _handler;

    public TicketClosedHandlerTests()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        var ticketService = new TicketService(_tickets.Object, _policies.Object, _unitOfWork.Object);
        _handler = new TicketClosedHandler(ticketService);
    }

    [Fact]
    public async Task Handle_WithValidTicket_ShouldCalculateFeeAndPersist()
    {
        var entryAt = DateTime.UtcNow.AddHours(-2);
        var ticketId = Guid.NewGuid();
        var ticket = Ticket.OpenFromAccess(ticketId, "ABC1234", entryAt);
        var policy = PricingPolicy.Define(Guid.NewGuid(), Money.Create(10m), Money.Create(4m),
            60, 0, entryAt.AddDays(-1));
        var exitAt = DateTime.UtcNow;

        _tickets.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _policies.Setup(r => r.GetEffectiveAtAsync(It.IsAny<DateTime>())).ReturnsAsync(policy);
        _tickets.Setup(r => r.UpdateAsync(ticket)).Returns(Task.CompletedTask);

        var @event = new TicketClosedIntegrationEvent(ticketId, "ABC1234", exitAt, Guid.NewGuid());

        await _handler.Handle(@event, CancellationToken.None);

        ticket.Status.Should().Be(TicketStatus.Closed);
        _tickets.Verify(r => r.UpdateAsync(ticket), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoPricingPolicyDefined_ShouldThrowDomainException()
    {
        var ticketId = Guid.NewGuid();
        var ticket = Ticket.OpenFromAccess(ticketId, "ABC1234", DateTime.UtcNow.AddHours(-1));

        _tickets.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _policies.Setup(r => r.GetEffectiveAtAsync(It.IsAny<DateTime>())).ReturnsAsync((PricingPolicy?)null);

        var @event = new TicketClosedIntegrationEvent(ticketId, "ABC1234", DateTime.UtcNow, Guid.NewGuid());

        var act = async () => await _handler.Handle(@event, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Owner must define*");
    }
}
