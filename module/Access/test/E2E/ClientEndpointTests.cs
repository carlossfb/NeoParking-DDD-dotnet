using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using main.common.dto;
using main.application.service;
using main.infrastructure;
using main.infrastructure.persistence.mysql;

namespace Access.Tests.E2E;

// Teste E2E simplificado sem WebApplicationFactory
// Para testes completos de API, seria necessário referenciar o projeto web
public class ClientEndToEndTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ClientServiceImpl _service;

    public ClientEndToEndTests()
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
    public async Task CompleteClientWorkflow_ShouldWorkEndToEnd()
    {
        // Create
        var createRequest = new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null);
        var created = await _service.CreateClientAsync(createRequest);
        
        created.Should().NotBeNull();
        created.Name.Should().Be("João Silva");

        // Read
        var retrieved = await _service.GetClientByIdAsync(created.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("João Silva");

        // List
        var allClients = await _service.GetAllClientsAsync();
        allClients.Should().HaveCount(1);

        // Delete
        await _service.DeleteClientAsync(created.Id);
        var afterDelete = await _service.GetClientByIdAsync(created.Id);
        afterDelete.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}