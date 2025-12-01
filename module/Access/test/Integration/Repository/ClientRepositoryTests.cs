using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using main.domain.entity;
using main.domain.vo;
using main.infrastructure;
using main.infrastructure.persistence.mysql;

namespace Access.Tests.Integration.Repository;

public class ClientRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly MysqlRepositoryImpl _repository;

    public ClientRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new MysqlRepositoryImpl(_context);
    }

    [Fact]
    public async Task AddAsync_WithValidClient_ShouldPersistClient()
    {
        var client = Client.Create("João Silva", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));

        await _repository.AddAsync(client);
        var result = await _repository.GetByIdAsync(client.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("João Silva");
        result.Cpf.Document.Should().Be("11144477735");
    }

    [Fact]
    public async Task GetAllAsync_WithMultipleClients_ShouldReturnAllClients()
    {
        var client1 = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        var client2 = Client.Create("Maria", PhoneNumber.Create("+5521987654321"), Cpf.Create("98765432100"));

        await _repository.AddAsync(client1);
        await _repository.AddAsync(client2);

        var result = await _repository.GetAllAsync();

        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "João");
        result.Should().Contain(c => c.Name == "Maria");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingClient_ShouldRemoveClient()
    {
        var client = Client.Create("João", PhoneNumber.Create("+5511999999999"), Cpf.Create("11144477735"));
        await _repository.AddAsync(client);

        await _repository.DeleteAsync(client);
        var result = await _repository.GetByIdAsync(client.Id);

        result.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}