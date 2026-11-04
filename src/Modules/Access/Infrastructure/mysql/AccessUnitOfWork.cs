namespace NeoParking.Access.Infrastructure;

using NeoParking.Access.Application;
using NeoParking.Shared.Kernel.Persistence;

public sealed class AccessUnitOfWork : IUnitOfWork
{
    private readonly AccessDbContext _context;

    public AccessUnitOfWork(AccessDbContext context)
        => _context = context;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesOrConflictAsync(cancellationToken);
}
