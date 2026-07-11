namespace NeoParking.Billing.Tests.Unit.Application.EventHandlers;

using FluentAssertions;
using Moq;
using NeoParking.Billing.Application;
using NeoParking.Billing.Application.EventHandlers;
using NeoParking.Billing.Domain;
using NeoParking.Shared.Kernel.Events;

public class TicketOpenedHandlerTests
{
    private readonly Mock<ITicketRepository> _tickets = new();
    private readonly Mock<IPricingPolicyRepository> _policies = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly TicketOpenedHandler _handler;

    public TicketOpenedHandlerTests()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        var ticketService = new TicketService(_tickets.Object, _policies.Object, _unitOfWork.Object);
        _handler = new TicketOpenedHandler(ticketService);
    }

    [Fact]
    public async Task Handle_ShouldDelegateToTicketService_AndCreateLocalTicket()
    {
        var ticketId = Guid.NewGuid();
        var entryAt = DateTime.UtcNow;
        var @event = new TicketOpenedIntegrationEvent(ticketId, "ABC1234", null, entryAt, Guid.NewGuid());

        _tickets.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);
        _tickets.Setup(r => r.AddAsync(It.IsAny<Ticket>())).Returns(Task.CompletedTask);

        await _handler.Handle(@event, CancellationToken.None);

        _tickets.Verify(r => r.AddAsync(It.Is<Ticket>(t => t.Id == ticketId && t.Plate == "ABC1234")), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
