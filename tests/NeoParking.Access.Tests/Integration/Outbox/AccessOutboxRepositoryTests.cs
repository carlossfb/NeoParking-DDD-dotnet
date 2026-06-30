namespace NeoParking.Access.Tests.Integration.Outbox;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Infrastructure;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Outbox;

public class AccessOutboxRepositoryTests : IDisposable
{
    private record TestEvent(string Name, Guid CorrelationId) : IDomainEvent;

    private readonly AccessDbContext _context;
    private readonly AccessOutboxRepository _repository;

    public AccessOutboxRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AccessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AccessDbContext(options);
        _repository = new AccessOutboxRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistMessage()
    {
        var message = OutboxMessage.Create(new TestEvent("client-registered", Guid.NewGuid()));

        await _repository.AddAsync(message);
        await _context.SaveChangesAsync();

        var pending = await _repository.GetPendingAsync();
        pending.Should().HaveCount(1);
        pending[0].Type.Should().Contain(nameof(TestEvent));
    }

    [Fact]
    public async Task GetPendingAsync_ShouldReturnOnlyUnpublished()
    {
        var published = OutboxMessage.Create(new TestEvent("already-published", Guid.NewGuid()));
        var pending   = OutboxMessage.Create(new TestEvent("not-yet-published", Guid.NewGuid()));

        await _repository.AddAsync(published);
        await _repository.AddAsync(pending);
        await _context.SaveChangesAsync();

        await _repository.MarkAsPublishedAsync([published]);

        var result = await _repository.GetPendingAsync();

        result.Should().HaveCount(1);
        result[0].Payload.Should().Contain("not-yet-published");
    }

    [Fact]
    public async Task GetPendingAsync_ShouldRespectBatchSize()
    {
        for (var i = 0; i < 10; i++)
        {
            await _repository.AddAsync(OutboxMessage.Create(new TestEvent($"event-{i}", Guid.NewGuid())));
        }
        await _context.SaveChangesAsync();

        var result = await _repository.GetPendingAsync(batchSize: 3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task MarkAsPublishedAsync_ShouldSetPublishedAt()
    {
        var message = OutboxMessage.Create(new TestEvent("some-event", Guid.NewGuid()));
        await _repository.AddAsync(message);
        await _context.SaveChangesAsync();

        var pending = await _repository.GetPendingAsync();
        await _repository.MarkAsPublishedAsync(pending);

        var remaining = await _repository.GetPendingAsync();
        remaining.Should().BeEmpty();
    }

    public void Dispose() => _context.Dispose();
}
