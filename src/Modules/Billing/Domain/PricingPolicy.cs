namespace NeoParking.Billing.Domain;

using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Primitives;

/// <summary>
/// Aggregate root do Billing BC.
/// Representa a política de tarifação definida pelo Owner: uma taxa mínima (que cobre
/// um período inicial de permanência) + um valor cobrado por hora excedente.
///
/// Cada vez que o Owner redefine a tarifação, uma NOVA revisão é criada — a anterior
/// é encerrada (<see cref="EffectiveTo"/> preenchido), nunca sobrescrita. É esse
/// mecanismo de versionamento por período de vigência que permite reconstruir
/// historicamente qual tarifa estava valendo em qualquer instante do passado,
/// e não apenas qual é a tarifa vigente hoje.
/// </summary>
public sealed class PricingPolicy : Entity
{
    public Guid OwnerId { get; private set; }
    public Money MinimumFee { get; private set; }
    public Money HourlyRate { get; private set; }

    /// <summary>Quantos minutos de permanência já estão cobertos pela taxa mínima (ex: 60 = primeira hora).</summary>
    public int MinimumFeeCoverageMinutes { get; private set; }

    /// <summary>Período de carência (em minutos) em que a permanência é totalmente gratuita.</summary>
    public int GracePeriodMinutes { get; private set; }

    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }

    /// <summary>Verdadeiro enquanto esta revisão for a vigente (ainda não foi substituída).</summary>
    public bool IsActive => EffectiveTo is null;

    // Construtor para EF Core
    private PricingPolicy() : base(Guid.Empty)
    {
        MinimumFee = null!;
        HourlyRate = null!;
    }

    private PricingPolicy(
        Guid ownerId,
        Money minimumFee,
        Money hourlyRate,
        int minimumFeeCoverageMinutes,
        int gracePeriodMinutes,
        DateTime effectiveFrom) : base()
    {
        Validate(minimumFee, hourlyRate, minimumFeeCoverageMinutes, gracePeriodMinutes);

        OwnerId                   = ownerId;
        MinimumFee                = minimumFee;
        HourlyRate                = hourlyRate;
        MinimumFeeCoverageMinutes = minimumFeeCoverageMinutes;
        GracePeriodMinutes        = gracePeriodMinutes;
        EffectiveFrom             = effectiveFrom;
        EffectiveTo               = null;
    }

    private PricingPolicy(
        Guid id,
        Guid ownerId,
        Money minimumFee,
        Money hourlyRate,
        int minimumFeeCoverageMinutes,
        int gracePeriodMinutes,
        DateTime effectiveFrom,
        DateTime? effectiveTo) : base(id)
    {
        OwnerId                   = ownerId;
        MinimumFee                = minimumFee;
        HourlyRate                = hourlyRate;
        MinimumFeeCoverageMinutes = minimumFeeCoverageMinutes;
        GracePeriodMinutes        = gracePeriodMinutes;
        EffectiveFrom             = effectiveFrom;
        EffectiveTo               = effectiveTo;
    }

    // ── factory methods ──────────────────────────────────────────────────────

    /// <summary>
    /// Define uma nova revisão de tarifação a partir de agora (ou de um instante específico).
    /// Não encerra revisões anteriores — isso é responsabilidade da Application, que
    /// coordena o fechamento da revisão vigente antes de criar esta.
    /// </summary>
    public static PricingPolicy Define(
        Guid ownerId,
        Money minimumFee,
        Money hourlyRate,
        int minimumFeeCoverageMinutes = 60,
        int gracePeriodMinutes = 0,
        DateTime? effectiveFrom = null)
        => new(ownerId, minimumFee, hourlyRate, minimumFeeCoverageMinutes,
               gracePeriodMinutes, effectiveFrom ?? DateTime.UtcNow);

    /// <summary>Reconstitui a partir do banco sem revalidar nem gerar novo Id.</summary>
    public static PricingPolicy Reconstitute(
        Guid id,
        Guid ownerId,
        Money minimumFee,
        Money hourlyRate,
        int minimumFeeCoverageMinutes,
        int gracePeriodMinutes,
        DateTime effectiveFrom,
        DateTime? effectiveTo)
        => new(id, ownerId, minimumFee, hourlyRate, minimumFeeCoverageMinutes,
               gracePeriodMinutes, effectiveFrom, effectiveTo);

    // ── comportamentos ───────────────────────────────────────────────────────

    /// <summary>Encerra esta revisão — chamado quando o Owner define uma nova política.</summary>
    public void Close(DateTime effectiveTo)
    {
        if (!IsActive)
            throw new DomainException("Pricing policy is already closed.");

        if (effectiveTo <= EffectiveFrom)
            throw new DomainException("EffectiveTo must be after EffectiveFrom.");

        EffectiveTo = effectiveTo;
    }

    /// <summary>Verifica se esta revisão estava vigente em um determinado instante — base da consulta histórica.</summary>
    public bool WasEffectiveAt(DateTime moment)
        => moment >= EffectiveFrom && (EffectiveTo is null || moment < EffectiveTo.Value);

    // ── validações ───────────────────────────────────────────────────────────

    private static void Validate(Money minimumFee, Money hourlyRate, int minimumFeeCoverageMinutes, int gracePeriodMinutes)
    {
        if (minimumFee is null || hourlyRate is null)
            throw new DomainException("Minimum fee and hourly rate are required.");

        // Um corpo vazio no POST vira 0 e 0 pelo binding: sem isso, a tarifa vigente passava a ser grátis.
        if (minimumFee.Value <= 0 || hourlyRate.Value <= 0)
            throw new DomainException("Minimum fee and hourly rate must be greater than zero.");

        if (minimumFeeCoverageMinutes <= 0)
            throw new DomainException("Minimum fee coverage must be greater than zero minutes.");

        if (gracePeriodMinutes < 0)
            throw new DomainException("Grace period cannot be negative.");
    }
}
