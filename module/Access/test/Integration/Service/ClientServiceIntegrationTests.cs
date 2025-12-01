using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using main.common.dto;
using main.application.service;
using main.domain.exception;
using main.infrastructure;
using main.infrastructure.persistence.mysql;

namespace Access.Tests.Integration.Service;

public class ClientServiceIntegrationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ClientServiceImpl _service;

    public ClientServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        var repository = new MysqlRepositoryImpl(_context);
        var mapper = new main.infrastructure.util.ClientMapper();
        _service = new ClientServiceImpl(repository, mapper);
    }

    [Fact]
    public async Task CreateClientAsync_WithValidData_ShouldPersistAndReturnClient()
    {
        var request = new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null);

        var result = await _service.CreateClientAsync(request);

        result.Should().NotBeNull();
        result.Name.Should().Be("João Silva");
        
        var allClients = await _service.GetAllClientsAsync();
        allClients.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteClientAsync_WithNonExistingId_ShouldThrowDomainException()
    {
        var nonExistingId = Guid.NewGuid();

        var act = async () => await _service.DeleteClientAsync(nonExistingId);
        await act.Should().ThrowAsync<DomainException>().WithMessage("Client not found");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}