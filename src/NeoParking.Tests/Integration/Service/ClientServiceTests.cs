// Integration/Service/ClientServiceIntegrationTests.cs
namespace NeoParking.Access.Tests.Integration.Service;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Application;
using NeoParking.Access.Infrastructure;
using NeoParking.Shared.Kernel.Exceptions;

public class ClientServiceIntegrationTests : IDisposable
{
    private readonly AccessDbContext _context;
    private readonly ClientService _service;

    public ClientServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AccessDbContext(options);
        _service = new ClientService(new MySqlClientRepository(_context));
    }

    [Fact]
    public async Task CreateClientAsync_ShouldPersistAndReturnClient()
    {
        var result = await _service.CreateClientAsync(
            new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null));

        result.Should().NotBeNull();
        result.Name.Should().Be("João Silva");
        (await _service.GetAllClientsAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteClientAsync_WithNonExistingId_ShouldThrowDomainException()
    {
        var act = async () => await _service.DeleteClientAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>().WithMessage("Client not found");
    }

    public void Dispose() => _context.Dispose();
}