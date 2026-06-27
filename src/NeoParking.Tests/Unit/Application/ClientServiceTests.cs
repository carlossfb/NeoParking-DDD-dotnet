// Unit/Application/ClientServiceTests.cs
namespace NeoParking.Access.Tests.Unit.Application;

using FluentAssertions;
using Moq;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Exceptions;

public class ClientServiceTests
{
    private readonly Mock<IClientRepository> _mockRepository = new();
    private readonly ClientService _service;

    public ClientServiceTests() =>
        _service = new ClientService(_mockRepository.Object);

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
    public async Task GetClientByIdAsync_WithNonExistingId_ShouldReturnNull()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Client?)null);

        var result = await _service.GetClientByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteClientAsync_WithNonExistingId_ShouldThrowDomainException()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Client?)null);

        var act = async () => await _service.DeleteClientAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>().WithMessage("Client not found");
    }
}