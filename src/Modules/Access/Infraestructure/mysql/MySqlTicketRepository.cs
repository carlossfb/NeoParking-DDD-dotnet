namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Primitives;

public sealed class MySqlTicketRepository : ITicketRepository
{
    private readonly AccessDbContext _context;

    public MySqlTicketRepository(AccessDbContext context) =>
        _context = context;

    public async Task AddAsync(Ticket ticket)
        // Sem SaveChangesAsync — quem chama (TicketService) é responsável pelo SaveChanges atômico
        => await _context.Tickets.AddAsync(ticket);

    public Task UpdateAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        return Task.CompletedTask;
    }

    public Task<Ticket?> GetByIdAsync(Guid ticketId)
        => _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);

    public Task<Ticket?> GetOpenByPlateAsync(string plate)
    {
        var normalized = Plate.Create(plate);

        return _context.Tickets
            .FirstOrDefaultAsync(t => t.Plate == normalized && t.Status == TicketStatus.Open);
    }

    public async Task<PagedResult<Ticket>> ListAsync(
        TicketStatus? status,
        string? plate,
        DateTime? entryFrom,
        DateTime? entryTo,
        int page,
        int pageSize)
    {
        var query = _context.Tickets.AsNoTracking();

        if (status is not null)
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(plate))
        {
            // Busca parcial: normaliza sem validar formato (entrada pode não ser uma placa completa).
            var normalizedPlate = Plate.Normalize(plate);
            query = query.Where(t => t.Plate.Value.Contains(normalizedPlate));
        }

        if (entryFrom is not null)
            query = query.Where(t => t.EntryAt >= entryFrom);

        if (entryTo is not null)
            query = query.Where(t => t.EntryAt <= entryTo);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.EntryAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Ticket>(items, page, pageSize, totalCount);
    }
}
