// Integration/Repository/ClientRepositoryTests.cs
namespace NeoParking.Access.Tests.Integration.Repository;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Domain;
using NeoParking.Access.Infrastructure;

public class ClientRepositoryTests : IDisposable
{
    private readonly AccessDbContext _context;
    private readonly MySqlClientRepository _repository;

    public ClientRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AccessDbContext(options);
        _repository = new MySqlClientRepository(_context);
    }

    [Fact]
    public async Task AddAsync_WithValidClient_ShouldPersistClient()
    {
        var client = Client.Create("João Silva", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));

        await _repository.AddAsync(client);
        var result = await _repository.GetByIdAsync(client.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("João Silva");
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleClients_ShouldReturnAll()
    {
        await _repository.AddAsync(Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735")));
        await _repository.AddAsync(Client.Create("Maria", PhoneNumber.Create("+5521987654321"), Cpf.Create("98765432100")));

        var result = await _repository.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingClient_ShouldRemoveClient()
    {
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        await _repository.AddAsync(client);

        await _repository.DeleteAsync(client);

        (await _repository.GetByIdAsync(client.Id)).Should().BeNull();
    }

    public void Dispose() => _context.Dispose();
}