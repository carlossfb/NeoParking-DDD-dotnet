namespace NeoParking.Shared.Kernel.Outbox;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implementação genérica de IOutboxRepository para qualquer DbContext do EF Core que
/// exponha um DbSet&lt;OutboxMessage&gt; chamado "OutboxMessages". Cada módulo continua expondo
/// sua própria interface marker (ex.: IAccessOutboxRepository : IOutboxRepository) para manter
/// um tipo distinto no DI — este genérico só concentra a lógica de acesso a dados, idêntica
/// em todos os módulos.
/// </summary>
public sealed class OutboxRepository<TContext> : IOutboxRepository where TContext : DbContext
{
    private readonly TContext _context;

    public OutboxRepository(TContext context)
        => _context = context;

    public async Task AddAsync(OutboxMessage message)
        // Sem SaveChangesAsync — quem chama coordena o SaveChanges atômico via IUnitOfWork,
        // junto com o restante da transação (aggregate + outbox).
        => await _context.Set<OutboxMessage>().AddAsync(message);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 50)
        => await _context.Set<OutboxMessage>()
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync();

    public async Task MarkAsPublishedAsync(IEnumerable<OutboxMessage> messages)
    {
        foreach (var message in messages)
            message.MarkAsPublished();

        await _context.SaveChangesAsync();
    }
}
