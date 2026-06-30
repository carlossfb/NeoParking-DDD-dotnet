namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using NeoParking.Shared.Kernel.Outbox;

public sealed class AccessOutboxRepository : IOutboxRepository
{
    private readonly AccessDbContext _context;

    public AccessOutboxRepository(AccessDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message)
    {
        await _context.OutboxMessages.AddAsync(message);
        // sem SaveChanges — quem chama é responsável pela transação
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 50)
    {
        return await _context.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task MarkAsPublishedAsync(IEnumerable<OutboxMessage> messages)
    {
        foreach (var message in messages)
            message.MarkAsPublished();

        await _context.SaveChangesAsync();
    }
}
