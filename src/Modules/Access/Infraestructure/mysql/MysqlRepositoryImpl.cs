namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Domain;

public sealed class MySqlClientRepository : IClientRepository
{
    private readonly AccessDbContext _context;

    public MySqlClientRepository(AccessDbContext context) =>
        _context = context;

    public async Task AddAsync(Client client)
    {
        await _context.Clients.AddAsync(client);
        await _context.SaveChangesAsync();
    }

    public async Task<Client?> GetByIdAsync(Guid clientId) =>
        await _context.Clients
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == clientId);

    public async Task<IEnumerable<Client>> GetAllAsync() =>
        await _context.Clients
            .AsNoTracking()
            .Include(c => c.Vehicles)
            .ToListAsync();

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

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Client client)
    {
        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();
    }
}