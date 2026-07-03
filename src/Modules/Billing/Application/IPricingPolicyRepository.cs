namespace NeoParking.Billing.Application;

using NeoParking.Billing.Domain;

public interface IPricingPolicyRepository
{
    Task AddAsync(PricingPolicy policy);
    Task UpdateAsync(PricingPolicy policy);
    Task<PricingPolicy?> GetByIdAsync(Guid id);

    /// <summary>A revisão atualmente vigente (EffectiveTo == null), se existir.</summary>
    Task<PricingPolicy?> GetActiveAsync();

    /// <summary>A revisão que estava vigente em um instante específico do passado (ou presente).</summary>
    Task<PricingPolicy?> GetEffectiveAtAsync(DateTime moment);

    /// <summary>Todas as revisões já definidas, da mais antiga para a mais recente — histórico completo de tarifação.</summary>
    Task<IReadOnlyList<PricingPolicy>> GetHistoryAsync();
}
