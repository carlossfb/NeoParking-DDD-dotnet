namespace NeoParking.Access.Tests.Unit.Application;

using FluentAssertions;
using Moq;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;
using NeoParking.Shared.Kernel.Outbox;

public class ClientServiceTests
{
    private readonly Mock<IClientRepository> _mockRepository = new();
    private readonly Mock<IOutboxRepository> _mockOutbox = new();
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<ICorrelationIdProvider> _mockCorrelation = new();
    private readonly ClientService _service;

    public ClientServiceTests()
    {
        _mockCorrelation.Setup(c => c.GetCorrelationId()).Returns(Guid.NewGuid());
        _mockOutbox.Setup(o => o.AddAsync(It.IsAny<OutboxMessage>())).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        _service = new ClientService(
            _mockRepository.Object,
            _mockOutbox.Object,
            _mockUnitOfWork.Object,
            _mockCorrelation.Object);
    }

    [Fact]
    public async Task CreateClientAsync_WithValidData_ShouldReturnClientResponse()
    {
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Client>())).Returns(Task.CompletedTask);

        var result = await _service.CreateClientAsync(
            new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null));

        result.Should().NotBeNull();
        result.Name.Should().Be("João Silva");
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Client>()), Times.Once);
    }

    [Fact]
    public async Task CreateClientAsync_ShouldEnqueueOutboxMessage()
    {
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Client>())).Returns(Task.CompletedTask);

        await _service.CreateClientAsync(
            new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null));

        _mockOutbox.Verify(o => o.AddAsync(It.IsAny<OutboxMessage>()), Times.Once);
    }

    [Fact]
    public async Task CreateClientAsync_ShouldCommitUnitOfWork()
    {
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Client>())).Returns(Task.CompletedTask);

        await _service.CreateClientAsync(
            new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null));

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateClientAsync_ShouldUseCorrelationIdFromProvider()
    {
        var expectedCorrelationId = Guid.NewGuid();
        _mockCorrelation.Setup(c => c.GetCorrelationId()).Returns(expectedCorrelationId);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Client>())).Returns(Task.CompletedTask);

        await _service.CreateClientAsync(
            new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null));

        _mockCorrelation.Verify(c => c.GetCorrelationId(), Times.Once);
    }

    [Fact]
    public async Task GetClientByIdAsync_WithNonExistingId_ShouldReturnNull()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Client?)null);

        var result = await _service.GetClientByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteClientAsync_WithNonExistingId_ShouldThrowNotFoundException()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Client?)null);

        var act = async () => await _service.DeleteClientAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void RegisterVehicle_WithDuplicatePlate_ShouldThrowDomainException()
    {
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        client.RegisterVehicle(Plate.Create("ABC1234"));

        var act = () => client.RegisterVehicle(Plate.Create("ABC1234"));

        act.Should().Throw<DomainException>().WithMessage("*ABC1234*");
    }
}
