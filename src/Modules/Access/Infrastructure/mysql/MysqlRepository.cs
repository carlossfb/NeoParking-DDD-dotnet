namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Primitives;

public sealed class MySqlClientRepository : IClientRepository
{
    private readonly AccessDbContext _context;

    public MySqlClientRepository(AccessDbContext context) =>
        _context = context;

    public async Task AddAsync(Client client)
    {
        // Sem SaveChangesAsync — quem chama (ClientService) é responsável pelo SaveChanges atômico
        await _context.Clients.AddAsync(client);
    }

    public async Task<Client?> GetByIdAsync(Guid clientId) =>
        await _context.Clients
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == clientId);

    public async Task<Client?> GetByPlateAsync(Plate plate) =>
        await _context.Clients
            .AsNoTracking()
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Vehicles.Any(v => v.Plate == plate));

    public async Task<Client?> GetByDocumentHmacAsync(string documentHmac) =>
        await _context.Clients
            .AsNoTracking()
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.DocumentHmac == documentHmac);

    // A ordem é por id porque nome e telefone não servem para ordenar quando estiverem cifrados (TD-710).
    public async Task<PagedResult<Client>> ListAsync(PageRequest page)
    {
        var totalCount = await _context.Clients.CountAsync();

        var items = await _context.Clients
            .AsNoTracking()
            .Include(c => c.Vehicles)
            .OrderBy(c => c.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync();

        return new PagedResult<Client>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task UpdateAsync(Client client)
    {
        foreach (var vehicle in client.Vehicles)
        {
            var entry = _context.Entry(vehicle);

            if (entry.State == EntityState.Detached || entry.State == EntityState.Modified)
            {
                var exists = await _context.Vehicles
                    .AsNoTracking()
                    .AnyAsync(v => v.Id == vehicle.Id);

                if (exists)
                    entry.State = EntityState.Modified;
                else
                    entry.State = EntityState.Added;
            }
        }

        var vehicleIds = client.Vehicles.Select(v => v.Id).ToList();
        var orphans = await _context.Vehicles
            .Where(v => v.ClientId == client.Id && !vehicleIds.Contains(v.Id))
            .ToListAsync();

        if (orphans.Any())
            _context.Vehicles.RemoveRange(orphans);

    }

    public async Task AddDocumentAccessAsync(ClientDocumentAccess access) =>
        await _context.ClientDocumentAccesses.AddAsync(access);

    public async Task<PagedResult<ClientDocumentAccess>> ListDocumentAccessesAsync(Guid clientId, PageRequest page)
    {
        var query = _context.ClientDocumentAccesses.AsNoTracking().Where(a => a.ClientId == clientId);
        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.AccessedAt)
            .ThenBy(a => a.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync();

        return new PagedResult<ClientDocumentAccess>(items, page.Page, page.PageSize, totalCount);
    }

    public Task DeleteAsync(Client client)
    {
        _context.Clients.Remove(client);
        return Task.CompletedTask;

    }
}
