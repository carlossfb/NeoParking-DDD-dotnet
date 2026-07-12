namespace NeoParking.Management.Infrastructure;

using NeoParking.Management.Application;
using NeoParking.Shared.Kernel.Outbox;

public sealed class ManagementOutboxRepository : IManagementOutboxRepository
{
    private readonly OutboxRepository<ManagementDbContext> _inner;

    public ManagementOutboxRepository(ManagementDbContext context)
        => _inner = new OutboxRepository<ManagementDbContext>(context);

    public Task AddAsync(OutboxMessage message)
        => _inner.AddAsync(message);

    public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 50)
        => _inner.GetPendingAsync(batchSize);

    public Task MarkAsPublishedAsync(IEnumerable<OutboxMessage> messages)
        => _inner.MarkAsPublishedAsync(messages);
}