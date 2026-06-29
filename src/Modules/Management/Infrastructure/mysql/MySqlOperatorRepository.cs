namespace NeoParking.Management.Infrastructure;

using Microsoft.EntityFrameworkCore;
using NeoParking.Management.Application;
using NeoParking.Management.Domain;

public sealed class MySqlOperatorRepository : IOperatorRepository
{
    private readonly ManagementDbContext _context;

    public MySqlOperatorRepository(ManagementDbContext context)
        => _context = context;

    public async Task AddAsync(OperatorUser user)
    {
        await _context.Operators.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task<OperatorUser?> GetByIdAsync(Guid id)
        => await _context.Operators.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<OperatorUser?> GetByEmailAsync(string email)
        => await _context.Operators
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

    public async Task UpdateAsync(OperatorUser user)
    {
        _context.Operators.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsOwnerAsync()
        => await _context.Operators
            .AnyAsync(u => u.Role == OperatorRole.Owner);
}
