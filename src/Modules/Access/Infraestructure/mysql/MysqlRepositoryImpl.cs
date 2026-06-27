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
            .AsNoTracking()
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == clientId);

    public async Task<IEnumerable<Client>> GetAllAsync() =>
        await _context.Clients
            .AsNoTracking()
            .Include(c => c.Vehicles)
            .ToListAsync();

    public async Task UpdateAsync(Client client)
    {
        _context.Clients.Update(client);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Client client)
    {
        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();
    }
}