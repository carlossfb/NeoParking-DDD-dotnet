namespace NeoParking.Billing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using NeoParking.Billing.Application;
using NeoParking.Billing.Domain;

public sealed class MySqlTicketRepository : ITicketRepository
{
    private readonly BillingDbContext _context;

    public MySqlTicketRepository(BillingDbContext context) => _context = context;

    public async Task AddAsync(Ticket ticket)
        // Sem SaveChangesAsync — quem chama coordena o SaveChanges atômico via IUnitOfWork.
        => await _context.Tickets.AddAsync(ticket);

    public Task UpdateAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        return Task.CompletedTask;
    }

    public Task<Ticket?> GetByIdAsync(Guid id)
        => _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IReadOnlyList<Ticket>> GetFeeHistoryAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Tickets
            .AsNoTracking()
            .Where(t => t.Status == TicketStatus.Closed);

        if (from is not null)
            query = query.Where(t => t.FeeCalculatedAt >= from);

        if (to is not null)
            query = query.Where(t => t.FeeCalculatedAt <= to);

        return await query
            .OrderByDescending(t => t.FeeCalculatedAt)
            .ToListAsync();
    }
}
