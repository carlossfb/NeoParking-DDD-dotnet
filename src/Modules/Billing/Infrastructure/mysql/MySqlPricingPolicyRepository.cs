namespace NeoParking.Billing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using NeoParking.Billing.Application;
using NeoParking.Billing.Domain;

public sealed class MySqlPricingPolicyRepository : IPricingPolicyRepository
{
    private readonly BillingDbContext _context;

    public MySqlPricingPolicyRepository(BillingDbContext context) => _context = context;

    public async Task AddAsync(PricingPolicy policy)
        // Sem SaveChangesAsync — quem chama (PricingPolicyService) coordena o SaveChanges
        // atômico via IUnitOfWork, junto com o encerramento da revisão anterior e o outbox.
        => await _context.PricingPolicies.AddAsync(policy);

    public Task UpdateAsync(PricingPolicy policy)
    {
        _context.PricingPolicies.Update(policy);
        return Task.CompletedTask;
    }

    public Task<PricingPolicy?> GetByIdAsync(Guid id)
        => _context.PricingPolicies.FirstOrDefaultAsync(p => p.Id == id);

    public Task<PricingPolicy?> GetActiveAsync()
        => _context.PricingPolicies.FirstOrDefaultAsync(p => p.EffectiveTo == null);

    public Task<PricingPolicy?> GetEffectiveAtAsync(DateTime moment)
        => _context.PricingPolicies
            .Where(p => p.EffectiveFrom <= moment && (p.EffectiveTo == null || moment < p.EffectiveTo))
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<PricingPolicy>> GetHistoryAsync()
        => await _context.PricingPolicies
            .AsNoTracking()
            .OrderBy(p => p.EffectiveFrom)
            .ToListAsync();
}
