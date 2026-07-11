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
       
    }

    public async Task<OperatorUser?> GetByIdAsync(Guid id)
        => await _context.Operators.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<OperatorUser?> GetByEmailAsync(string email)
        => await _context.Operators
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

    public Task UpdateAsync(OperatorUser user)
    {
        _context.Operators.Update(user);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsOwnerAsync()
        => await _context.Operators
            .AnyAsync(u => u.Role == OperatorRole.Owner);
}
