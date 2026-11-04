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

        (await act.Should().ThrowAsync<DomainException>()).Which.Message.Should().NotContain("ABC1234");
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

    // ── C2: código e saída por placa ─────────────────────────────────────────

    private const string UnknownCode = "0123456789ABCDEF0123";

    private void VerifyClosedEvents(Times times) =>
        _mockOutbox.Verify(o => o.AddAsync(It.Is<OutboxMessage>(
            m => m.Type == typeof(TicketClosedIntegrationEvent).FullName)), times);

    private Ticket OpenTicketFoundByCode()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: DateTime.UtcNow.AddHours(-1));
        _mockRepository.Setup(r => r.GetByCodeAsync(ticket.Code)).ReturnsAsync(ticket);
        return ticket;
    }

    private Ticket OpenTicketFoundByPlate()
    {
        var ticket = Ticket.RegisterEntry(Plate.Create("ABC1234"), entryAt: DateTime.UtcNow.AddHours(-1));
        _mockRepository.Setup(r => r.GetOpenByPlateAsync("ABC1234")).ReturnsAsync(ticket);
        return ticket;
    }

    [Fact]
    public async Task RegisterEntryAsync_ShouldReturnTheCode()
    {
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Ticket>())).Returns(Task.CompletedTask);

        var result = await _service.RegisterEntryAsync(new RegisterEntryRequest("ABC1234"));

        result.Code.Should().MatchRegex("^[0-9A-F]{20}$");
    }

    [Fact]
    public async Task GetByCodeAsync_ShouldFindTheTicket()
    {
        var ticket = OpenTicketFoundByCode();

        var result = await _service.GetByCodeAsync(ticket.Code.Value.ToLowerInvariant());

        result.Id.Should().Be(ticket.Id);
        result.Code.Should().Be(ticket.Code.Value);
    }

    [Theory]
    [InlineData(UnknownCode)]
    [InlineData("not-a-code")]
    public async Task UnknownOrMalformedCode_ShouldBeNotFound_WithoutEchoingIt(string code)
    {
        var lookup = async () => await _service.GetByCodeAsync(code);
        var exit = async () => await _service.RegisterExitByCodeAsync(code, new RegisterExitRequest());

        (await lookup.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().NotContain(code);
        (await exit.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().NotContain(code);
        VerifyClosedEvents(Times.Never());
    }

    [Fact]
    public async Task RegisterExitByCodeAsync_ShouldCloseAndPublishASingleEvent()
    {
        var ticket = OpenTicketFoundByCode();

        var result = await _service.RegisterExitByCodeAsync(ticket.Code.Value, new RegisterExitRequest());

        result.Status.Should().Be(nameof(TicketStatus.Closed));
        result.ExitMethod.Should().Be(nameof(ExitMethod.Code));
        VerifyClosedEvents(Times.Once());
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RegisterExitByCodeAsync_OnAClosedTicket_ShouldBeRejectedWithoutANewEvent()
    {
        var ticket = OpenTicketFoundByCode();
        ticket.RegisterExit();

        var act = async () => await _service.RegisterExitByCodeAsync(ticket.Code.Value, new RegisterExitRequest());

        (await act.Should().ThrowAsync<DomainException>()).Which.Should().NotBeOfType<ConflictException>();
        VerifyClosedEvents(Times.Never());
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task RegisterExitByPlateAsync_ShouldRecordReasonAndAuthor_AndPublishASingleEvent()
    {
        var ticket = OpenTicketFoundByPlate();
        var operatorId = Guid.NewGuid();

        var result = await _service.RegisterExitByPlateAsync(
            "abc-1234", new RegisterExitByPlateRequest("Ticket perdido"), operatorId);

        result.ExitMethod.Should().Be(nameof(ExitMethod.Plate));
        ticket.ExitReason.Should().Be("Ticket perdido");
        ticket.ExitOperatorId.Should().Be(operatorId);
        VerifyClosedEvents(Times.Once());
    }

    [Fact]
    public async Task RegisterExitByPlateAsync_WithoutReason_ShouldBeRejectedWithoutSaving()
    {
        OpenTicketFoundByPlate();

        var act = async () => await _service.RegisterExitByPlateAsync(
            "ABC1234", new RegisterExitByPlateRequest("  "), Guid.NewGuid());

        await act.Should().ThrowAsync<DomainException>();
        VerifyClosedEvents(Times.Never());
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task RegisterExitByPlateAsync_WithoutAnOpenTicket_ShouldBeNotFound_WithoutThePlateInTheMessage()
    {
        _mockRepository.Setup(r => r.GetOpenByPlateAsync("ABC1234")).ReturnsAsync((Ticket?)null);

        var act = async () => await _service.RegisterExitByPlateAsync(
            "ABC1234", new RegisterExitByPlateRequest("Ticket perdido"), Guid.NewGuid());

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Message.Should().NotContain("ABC1234");
    }

    [Fact]
    public async Task RegisterExitByPlateAsync_WithAnInvalidPlate_ShouldNotEchoItInTheMessage()
    {
        var act = async () => await _service.RegisterExitByPlateAsync(
            "SENSITIVE", new RegisterExitByPlateRequest("Ticket perdido"), Guid.NewGuid());

        (await act.Should().ThrowAsync<DomainException>()).Which.Message.Should().NotContain("SENSITIVE");
    }

    [Fact]
    public async Task ExitThatLosesARace_ShouldBeReportedAsAlreadyClosed()
    {
        var ticket = OpenTicketFoundByCode();
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(default))
            .ThrowsAsync(new ConflictException("The operation conflicts with existing data."));

        var act = async () => await _service.RegisterExitByCodeAsync(ticket.Code.Value, new RegisterExitRequest());

        (await act.Should().ThrowAsync<DomainException>()).Which.Should().NotBeOfType<ConflictException>();
    }
}
