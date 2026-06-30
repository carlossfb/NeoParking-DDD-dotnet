namespace NeoParking.Shared.Kernel.Outbox;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 50);
    Task MarkAsPublishedAsync(IEnumerable<OutboxMessage> messages);
}
