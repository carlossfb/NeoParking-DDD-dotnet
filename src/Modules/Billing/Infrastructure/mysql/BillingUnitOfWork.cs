namespace NeoParking.Billing.Infrastructure;

using NeoParking.Billing.Application;

public sealed class BillingUnitOfWork : IUnitOfWork
{
    private readonly BillingDbContext _context;

    public BillingUnitOfWork(BillingDbContext context) => _context = context;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
