namespace NeoParking.Access.Tests.Integration.Service;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeoParking.Access.Application;
using NeoParking.Access.Infrastructure;
using NeoParking.Access.Tests.Integration.Repository;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;

[Trait("Category", "Integration")]
public class ClientServiceIntegrationTests : IClassFixture<MySqlFixture>, IDisposable
{
    private readonly AccessDbContext _context;
    private readonly ClientService _service;

    public ClientServiceIntegrationTests(MySqlFixture fixture)
    {
        fixture.Context.Database.ExecuteSqlRaw("DELETE FROM outbox_messages");
        fixture.Context.Database.ExecuteSqlRaw("DELETE FROM clients");

        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseMySql(fixture.ConnectionString, ServerVersion.AutoDetect(fixture.ConnectionString))
            .Options;

        _context = new AccessDbContext(options);

        var mockCorrelation = new Mock<ICorrelationIdProvider>();
        mockCorrelation.Setup(c => c.GetCorrelationId()).Returns(Guid.NewGuid());

        _service = new ClientService(
            new MySqlClientRepository(_context),
            new AccessOutboxRepository(_context),
            new AccessUnitOfWork(_context),    // implementação real — sem violar arquitetura nos testes de integração
            mockCorrelation.Object);
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
    public async Task CreateClientAsync_ShouldPersistOutboxMessage()
    {
        await _service.CreateClientAsync(
            new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null));

        var outbox = new AccessOutboxRepository(_context);
        var pending = await outbox.GetPendingAsync();
        pending.Should().HaveCount(1);
        pending[0].Type.Should().Contain("ClientRegistered");
    }

    [Fact]
    public async Task CreateClientAsync_ShouldPersistClientAndOutboxInSameTransaction()
    {
        await _service.CreateClientAsync(
            new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null));

        var clients = await _context.Clients.CountAsync();
        var outboxMessages = await _context.OutboxMessages.CountAsync();

        clients.Should().Be(1);
        outboxMessages.Should().Be(1);
    }

    [Theory]
    [InlineData("outbox_messages")]
    [InlineData("clients")]
    public async Task CreateClientAsync_WhenAnyInsertFails_ShouldPersistNothing(string failingTable)
    {
        // Falha real, dentro do banco, em uma das escritas da transação.
        await _context.Database.ExecuteSqlRawAsync(
            $"CREATE TRIGGER force_failure BEFORE INSERT ON {failingTable} FOR EACH ROW "
            + "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'forced failure'");

        try
        {
            var act = () => _service.CreateClientAsync(
                new ClientRequestDTO("João Silva", "+5511999999999", "11144477735", null));

            await act.Should().ThrowAsync<DbUpdateException>();
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync("DROP TRIGGER force_failure");
        }

        (await _context.Clients.AsNoTracking().CountAsync()).Should().Be(0);
        (await _context.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteClientAsync_WithNonExistingId_ShouldThrowNotFoundException()
    {
        var act = async () => await _service.DeleteClientAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    public void Dispose() => _context.Dispose();
}
