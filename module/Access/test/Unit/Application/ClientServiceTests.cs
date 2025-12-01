using FluentAssertions;
using Moq;
using main.common.dto;
using main.application.service;
using main.domain.entity;
using main.domain.ports;
using main.domain.vo;
using main.domain.exception;

namespace Access.Tests.Unit.Application;

public class ClientServiceTests
{
    private readonly Mock<IClientRepository> _mockRepository;
    private readonly Mock<IClientMapper> _mockMapper;
    private readonly ClientServiceImpl _service;

    public ClientServiceTests()
    {
        _mockRepository = new Mock<IClientRepository>();
        _mockMapper = new Mock<IClientMapper>();
        _service = new ClientServiceImpl(_mockRepository.Object, _mockMapper.Object);
    }

    [Fact]
    public async Task CreateClientAsync_WithValidData_ShouldReturnClientResponse()
    {
        var request = new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null);
        var client = Client.Create("João Silva", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        var response = new ClientResponseDTO(client.Id, "João Silva", client.PhoneNumber.Value, client.Cpf.Document, new List<VehicleResponseDTO>());
        
        _mockMapper.Setup(m => m.RequestDTOToDomain(request)).Returns(client);
        _mockMapper.Setup(m => m.DomainToResponseDTO(client)).Returns(response);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Client>())).Returns(Task.CompletedTask);

        var result = await _service.CreateClientAsync(request);

        result.Should().NotBeNull();
        result.Name.Should().Be("João Silva");
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Client>()), Times.Once);
    }

    [Fact]
    public async Task GetClientByIdAsync_WithNonExistingId_ShouldReturnNull()
    {
        var clientId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByIdAsync(clientId)).ReturnsAsync((Client?)null);

        var result = await _service.GetClientByIdAsync(clientId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteClientAsync_WithNonExistingId_ShouldThrowException()
    {
        var clientId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByIdAsync(clientId)).ReturnsAsync((Client?)null);

        var act = async () => await _service.DeleteClientAsync(clientId);
        await act.Should().ThrowAsync<DomainException>().WithMessage("Client not found");
    }
}