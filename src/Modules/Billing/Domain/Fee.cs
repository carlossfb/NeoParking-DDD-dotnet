namespace NeoParking.Billing.Domain;

using NeoParking.Billing.Domain.Vo;

/// <summary>
/// Snapshot imutável da cobrança calculada para um Ticket.
/// Guarda os valores da <see cref="PricingPolicy"/> vigente NO MOMENTO do cálculo —
/// mesmo que o Owner altere a tarifação depois, o valor já cobrado neste ticket nunca
/// muda. É esse "congelamento" que garante rastreabilidade histórica real do que foi
/// efetivamente cobrado ao longo do tempo, e não apenas da tarifa configurada hoje.
/// </summary>
public sealed record Fee
{
    /// <summary>Revisão de PricingPolicy usada para este cálculo — permite auditar exatamente qual tarifa se aplicou.</summary>
    public Guid PricingPolicyId { get; }

    public Money MinimumFee { get; }
    public Money HourlyRate { get; }

    /// <summary>Quantidade de horas excedentes cobradas além da cobertura da taxa mínima.</summary>
    public int BilledHours { get; }

    /// <summary>Valor final cobrado (MinimumFee + HourlyRate * BilledHours, ou zero se dentro da carência).</summary>
    public Money Amount { get; }

    public DateTime CalculatedAt { get; }

    private Fee(Guid pricingPolicyId, Money minimumFee, Money hourlyRate,
        int billedHours, Money amount, DateTime calculatedAt)
    {
        PricingPolicyId = pricingPolicyId;
        MinimumFee      = minimumFee;
        HourlyRate      = hourlyRate;
        BilledHours     = billedHours;
        Amount          = amount;
        CalculatedAt    = calculatedAt;
    }

    public static Fee Create(Guid pricingPolicyId, Money minimumFee, Money hourlyRate,
        int billedHours, Money amount, DateTime calculatedAt)
        => new(pricingPolicyId, minimumFee, hourlyRate, billedHours, amount, calculatedAt);
}
