namespace NeoParking.Access.Infrastructure;

using NeoParking.Access.Application;

public sealed class AccessUnitOfWork : IUnitOfWork
{
    private readonly AccessDbContext _context;

    public AccessUnitOfWork(AccessDbContext context)
        => _context = context;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
