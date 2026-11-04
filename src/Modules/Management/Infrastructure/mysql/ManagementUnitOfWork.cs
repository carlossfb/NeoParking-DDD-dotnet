namespace NeoParking.Management.Infrastructure;

using NeoParking.Management.Application;
using NeoParking.Shared.Kernel.Persistence;

public sealed class ManagementUnitOfWork : IUnitOfWork
{
    private readonly ManagementDbContext _context;

    public ManagementUnitOfWork(ManagementDbContext context)
        => _context = context;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesOrConflictAsync(cancellationToken);
}