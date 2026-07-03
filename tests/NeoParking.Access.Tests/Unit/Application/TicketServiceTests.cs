namespace NeoParking.Access.Tests.Unit.Application;

using FluentAssertions;
using Moq;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;
using NeoParking.Shared.Kernel.Outbox;

public class TicketServiceTests
{
    private readonly Mock<ITicketRepository> _mockRepository = new();
    private readonly Mock<IAccessOutboxRepository> _mockOutbox = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<ICorrelationIdProvider> _mockCorrelation = new();
    private readonly TicketService _service;

    public TicketServiceTests()
    {
        _mockCorrelation.Setup(c => c.GetCorrelationId()).Returns(Guid.NewGuid());
        _mockOutbox.Setup(o => o.AddAsync(It.IsAny<OutboxMessage>())).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        _service = new TicketService(
            _mockRepository.Object,
            _mockOutbox.Object,
            _mockUnitOfWork.Object,
            _mockCorrelation.Object);
    }

    [Fact]
    public async Task RegisterEntryAsync_WithNoExistingOpenTicket_ShouldCreateTicket()
    {
        _mockRepository.Setup(r => r.GetOpenByPlateAsync("ABC1234")).ReturnsAsync((Ticket?)null);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Ticket>())).Returns(Task.CompletedTask);

        var result = await _service.RegisterEntryAsync(new RegisterEntryRequest("ABC1234"));

        result.Plate.Should().Be("ABC1234");
        result.Status.Should().Be(nameof(TicketStatus.Open));
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Ticket>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RegisterEntryAsync_ShouldEnqueueTicketOpenedIntegrationEvent()
    {
        _mockRepository.Setup(r => r.GetOpenByPlateAsync("ABC1234")).ReturnsAsync((Ticket?)null);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Ticket>())).Returns(Task.CompletedTask);

        await _service.RegisterEntryAsync(new RegisterEntryRequest("ABC1234"));

        _mockOutbox.Verify(o => o.AddAsync(It.Is<OutboxMessage>(
            m => m.Type == typeof(TicketOpenedIntegrationEvent).FullName)), Times.Once);
    }

    [Fact]
    public async Task RegisterEntryAsync_WithExistingOpenTicket_ShouldThrowDomainException()
    {
        var existing = Ticket.RegisterEntry(Plate.Create("ABC1234"));
        _mockRepository.Setup(r => r.GetOpenByPlateAsync("ABC1234")).ReturnsAsync(existing);

        var act = async () => await _service.RegisterEntryAsync(new RegisterEntryRequest("ABC1234"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*ABC1234*");
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Ticket>()), Times.Never);
    }

    [Fact]
    public async Task RegisterExitAsync_WithValidTicket_ShouldCloseTicketAndEnqueueEvent()
    {
        var entryAt = DateTime.UtcNow.AddHours(-1);
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: entryAt);

        _mockRepository.Setup(r => r.GetByIdAsync(ticket.Id)).ReturnsAsync(ticket);
        _mockRepository.Setup(r => r.UpdateAsync(ticket)).Returns(Task.CompletedTask);

        var result = await _service.RegisterExitAsync(ticket.Id, new RegisterExitRequest());

        result.Status.Should().Be(nameof(TicketStatus.Closed));
        _mockRepository.Verify(r => r.UpdateAsync(ticket), Times.Once);
        _mockOutbox.Verify(o => o.AddAsync(It.Is<OutboxMessage>(
            m => m.Type == typeof(TicketClosedIntegrationEvent).FullName)), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RegisterExitAsync_WithNonExistingTicket_ShouldThrowNotFoundException()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Ticket?)null);

        var act = async () => await _service.RegisterExitAsync(Guid.NewGuid(), new RegisterExitRequest());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
