namespace NeoParking.Billing.Tests.Unit.Application;

using FluentAssertions;
using Moq;
using NeoParking.Billing.Application;
using NeoParking.Billing.Domain;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;

public class TicketServiceTests
{
    private readonly Mock<ITicketRepository> _mockTickets = new();
    private readonly Mock<IPricingPolicyRepository> _mockPolicies = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly TicketService _service;

    public TicketServiceTests()
    {
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        _service = new TicketService(
            _mockTickets.Object,
            _mockPolicies.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task HandleTicketOpenedAsync_WithNewTicket_ShouldCreateLocalTicket()
    {
        var ticketId = Guid.NewGuid();
        var entryAt = DateTime.UtcNow;

        _mockTickets.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync((Ticket?)null);
        _mockTickets.Setup(r => r.AddAsync(It.IsAny<Ticket>())).Returns(Task.CompletedTask);

        await _service.HandleTicketOpenedAsync(new TicketOpenedIntegrationEvent(
            ticketId, "ABC1234", null, entryAt, Guid.NewGuid()));

        _mockTickets.Verify(r => r.AddAsync(It.Is<Ticket>(t => t.Id == ticketId && t.Plate == "ABC1234")), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task HandleTicketOpenedAsync_WhenTicketAlreadyExists_ShouldBeIdempotent()
    {
        var ticketId = Guid.NewGuid();
        var existing = Ticket.OpenFromAccess(ticketId, "ABC1234", DateTime.UtcNow);
        _mockTickets.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(existing);

        await _service.HandleTicketOpenedAsync(new TicketOpenedIntegrationEvent(
            ticketId, "ABC1234", null, DateTime.UtcNow, Guid.NewGuid()));

        _mockTickets.Verify(r => r.AddAsync(It.IsAny<Ticket>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task HandleTicketClosedAsync_WithValidTicket_ShouldCalculateFeeUsingPolicyEffectiveAtExit()
    {
        var entryAt = DateTime.UtcNow.AddHours(-2);
        var ticketId = Guid.NewGuid();
        var ticket = Ticket.OpenFromAccess(ticketId, "ABC1234", entryAt);
        var policy = PricingPolicy.Define(Guid.NewGuid(), Money.Create(10m), Money.Create(4m),
            60, 0, entryAt.AddDays(-1));
        var exitAt = DateTime.UtcNow;

        _mockTickets.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _mockPolicies.Setup(r => r.GetEffectiveAtAsync(It.IsAny<DateTime>())).ReturnsAsync(policy);
        _mockTickets.Setup(r => r.UpdateAsync(ticket)).Returns(Task.CompletedTask);

        await _service.HandleTicketClosedAsync(new TicketClosedIntegrationEvent(
            ticketId, "ABC1234", exitAt, Guid.NewGuid()));

        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ChargedFee.Should().NotBeNull();
        ticket.ChargedFee!.Amount.Value.Should().Be(18m); // 2h => 1h coberta pela mínima + 1h excedente: 10 + 4*1
        _mockTickets.Verify(r => r.UpdateAsync(ticket), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task HandleTicketClosedAsync_WhenTicketAlreadyClosed_ShouldBeIdempotent()
    {
        var entryAt = DateTime.UtcNow.AddHours(-2);
        var ticketId = Guid.NewGuid();
        var ticket = Ticket.OpenFromAccess(ticketId, "ABC1234", entryAt);
        var policy = PricingPolicy.Define(Guid.NewGuid(), Money.Create(10m), Money.Create(4m), 60, 0, entryAt.AddDays(-1));
        ticket.Close(entryAt.AddHours(1), policy);

        _mockTickets.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);

        await _service.HandleTicketClosedAsync(new TicketClosedIntegrationEvent(
            ticketId, "ABC1234", DateTime.UtcNow, Guid.NewGuid()));

        _mockTickets.Verify(r => r.UpdateAsync(It.IsAny<Ticket>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task HandleTicketClosedAsync_WithNonExistingTicket_ShouldThrowNotFoundException()
    {
        _mockTickets.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Ticket?)null);

        var act = async () => await _service.HandleTicketClosedAsync(new TicketClosedIntegrationEvent(
            Guid.NewGuid(), "ABC1234", DateTime.UtcNow, Guid.NewGuid()));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleTicketClosedAsync_WithNoPricingPolicyDefined_ShouldThrowDomainException()
    {
        var ticketId = Guid.NewGuid();
        var ticket = Ticket.OpenFromAccess(ticketId, "ABC1234", DateTime.UtcNow.AddHours(-1));
        _mockTickets.Setup(r => r.GetByIdAsync(ticketId)).ReturnsAsync(ticket);
        _mockPolicies.Setup(r => r.GetEffectiveAtAsync(It.IsAny<DateTime>())).ReturnsAsync((PricingPolicy?)null);

        var act = async () => await _service.HandleTicketClosedAsync(new TicketClosedIntegrationEvent(
            ticketId, "ABC1234", DateTime.UtcNow, Guid.NewGuid()));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Owner must define*");
    }

    [Fact]
    public async Task GetFeeHistoryAsync_ShouldReturnMappedResponses()
    {
        var entryAt = DateTime.UtcNow.AddDays(-1);
        var policy = PricingPolicy.Define(Guid.NewGuid(), Money.Create(10m), Money.Create(4m), 60, 0, entryAt.AddDays(-1));
        var ticket = Ticket.OpenFromAccess(Guid.NewGuid(), "ABC1234", entryAt);
        ticket.Close(entryAt.AddHours(2), policy);

        _mockTickets.Setup(r => r.GetFeeHistoryAsync(null, null))
            .ReturnsAsync(new List<Ticket> { ticket });

        var result = await _service.GetFeeHistoryAsync();

        result.Should().HaveCount(1);
        result[0].Fee.Should().NotBeNull();
        result[0].Fee!.PricingPolicyId.Should().Be(policy.Id);
    }
}
