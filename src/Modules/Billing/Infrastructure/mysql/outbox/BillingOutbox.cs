namespace NeoParking.Billing.Infrastructure;

using NeoParking.Billing.Application;
using NeoParking.Shared.Kernel.Outbox;

public sealed class BillingOutboxRepository : IBillingOutboxRepository
{
    private readonly OutboxRepository<BillingDbContext> _inner;

    public BillingOutboxRepository(BillingDbContext context)
        => _inner = new OutboxRepository<BillingDbContext>(context);

    public Task AddAsync(OutboxMessage message)
        => _inner.AddAsync(message);

    public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 50)
        => _inner.GetPendingAsync(batchSize);

    public Task MarkAsPublishedAsync(IEnumerable<OutboxMessage> messages)
        => _inner.MarkAsPublishedAsync(messages);
}