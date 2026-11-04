namespace NeoParking.Access.Infrastructure;

using NeoParking.Access.Application;
using NeoParking.Shared.Kernel.Outbox;

public sealed class AccessOutboxRepository : IAccessOutboxRepository
{
    private readonly OutboxRepository<AccessDbContext> _inner;

    public AccessOutboxRepository(AccessDbContext context)
        => _inner = new OutboxRepository<AccessDbContext>(context);

    public Task AddAsync(OutboxMessage message)
        => _inner.AddAsync(message);

    public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 50)
        => _inner.GetPendingAsync(batchSize);

    public Task MarkAsPublishedAsync(IEnumerable<OutboxMessage> messages)
        => _inner.MarkAsPublishedAsync(messages);
}