namespace NeoParking.Billing.Domain;

using NeoParking.Billing.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Primitives;

/// <summary>
/// Aggregate root do Billing BC. Representa a cobrança referente a um "Ticket" (o
/// registro de entrada/saída de um veículo — ver nomenclatura de domínio do README).
///
/// O registro de entrada e saída é responsabilidade do Access BC — que opera a cancela
/// fisicamente. Este agregado NUNCA abre ou fecha um ticket por conta própria: ele reage
/// aos eventos de integração publicados pelo Access
/// (<see cref="Shared.Kernel.Events.TicketOpenedIntegrationEvent"/> /
/// <see cref="Shared.Kernel.Events.TicketClosedIntegrationEvent"/>), usando o mesmo Id do
/// Ticket do Access para correlacionar os dois lados. O que é genuinamente do Billing é
/// calcular e congelar a Fee no momento do fechamento.
///
/// A Fee é calculada e "congelada" no momento do fechamento (<see cref="Close"/>), usando
/// a PricingPolicy vigente naquele instante. Alterações futuras na tarifação nunca afetam
/// tickets já fechados — os campos Fee* abaixo são a fonte de verdade do que foi
/// efetivamente cobrado, independente do que a política diz hoje.
/// </summary>
public sealed class Ticket : Entity
{
    public string Plate { get; private set; }
    public DateTime EntryAt { get; private set; }
    public DateTime? ExitAt { get; private set; }
    public TicketStatus Status { get; private set; }

    // Campos "congelados" da cobrança no momento do fechamento — nunca recalculados.
    public Guid? FeePricingPolicyId { get; private set; }
    public Money? FeeMinimumFee { get; private set; }
    public Money? FeeHourlyRate { get; private set; }
    public int? FeeBilledHours { get; private set; }
    public Money? FeeAmount { get; private set; }
    public DateTime? FeeCalculatedAt { get; private set; }

    /// <summary>Reconstrói a cobrança congelada como Value Object de leitura, se o ticket já foi encerrado.</summary>
    public Fee? ChargedFee => Status == TicketStatus.Closed
        ? Fee.Create(
            FeePricingPolicyId!.Value,
            FeeMinimumFee!,
            FeeHourlyRate!,
            FeeBilledHours!.Value,
            FeeAmount!,
            FeeCalculatedAt!.Value)
        : null;

    // Construtor para EF Core
    private Ticket() : base(Guid.Empty)
    {
        Plate = string.Empty;
    }

    private Ticket(Guid id, string plate, DateTime entryAt) : base(id)
    {
        ValidatePlate(plate);

        Plate   = Normalize(plate);
        EntryAt = entryAt;
        Status  = TicketStatus.Open;
    }

    private Ticket(
        Guid id,
        string plate,
        DateTime entryAt,
        DateTime? exitAt,
        TicketStatus status,
        Guid? feePricingPolicyId,
        Money? feeMinimumFee,
        Money? feeHourlyRate,
        int? feeBilledHours,
        Money? feeAmount,
        DateTime? feeCalculatedAt) : base(id)
    {
        Plate               = plate;
        EntryAt             = entryAt;
        ExitAt              = exitAt;
        Status              = status;
        FeePricingPolicyId  = feePricingPolicyId;
        FeeMinimumFee       = feeMinimumFee;
        FeeHourlyRate       = feeHourlyRate;
        FeeBilledHours      = feeBilledHours;
        FeeAmount           = feeAmount;
        FeeCalculatedAt     = feeCalculatedAt;
    }

    // ── factory methods ──────────────────────────────────────────────────────

    /// <summary>
    /// Cria a representação local (Billing) de um ticket a partir do
    /// <see cref="Shared.Kernel.Events.TicketOpenedIntegrationEvent"/> publicado pelo Access.
    /// O Id é o mesmo do Ticket original no Access — é essa igualdade de Id que correlaciona
    /// os dois lados sem acoplamento direto entre os BCs.
    /// </summary>
    public static Ticket OpenFromAccess(Guid ticketId, string plate, DateTime entryAt)
        => new(ticketId, plate, entryAt);

    /// <summary>Reconstitui a partir do banco sem revalidar nem gerar novo Id.</summary>
    public static Ticket Reconstitute(
        Guid id,
        string plate,
        DateTime entryAt,
        DateTime? exitAt,
        TicketStatus status,
        Guid? feePricingPolicyId,
        Money? feeMinimumFee,
        Money? feeHourlyRate,
        int? feeBilledHours,
        Money? feeAmount,
        DateTime? feeCalculatedAt)
        => new(id, plate, entryAt, exitAt, status, feePricingPolicyId, feeMinimumFee,
               feeHourlyRate, feeBilledHours, feeAmount, feeCalculatedAt);

    // ── comportamentos ───────────────────────────────────────────────────────

    /// <summary>
    /// Encerra o ticket e calcula a cobrança com base na PricingPolicy vigente no momento
    /// da saída. Chamado em reação ao <see cref="Shared.Kernel.Events.TicketClosedIntegrationEvent"/>
    /// publicado pelo Access — nunca por iniciativa direta do Billing. A Fee resultante fica
    /// congelada neste ticket — não é afetada por mudanças futuras na política.
    /// </summary>
    public void Close(DateTime exitAt, PricingPolicy pricingPolicy)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException("Ticket is already closed.");

        if (exitAt <= EntryAt)
            throw new DomainException("Exit time must be after entry time.");

        var fee = FeeCalculator.Calculate(pricingPolicy, EntryAt, exitAt);

        ExitAt              = exitAt;
        Status              = TicketStatus.Closed;
        FeePricingPolicyId  = fee.PricingPolicyId;
        FeeMinimumFee       = fee.MinimumFee;
        FeeHourlyRate       = fee.HourlyRate;
        FeeBilledHours      = fee.BilledHours;
        FeeAmount           = fee.Amount;
        FeeCalculatedAt     = fee.CalculatedAt;
    }

    // ── validações ───────────────────────────────────────────────────────────

    private static void ValidatePlate(string plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            throw new DomainException("Plate is required.");
    }

    private static string Normalize(string plate)
        => plate.Trim().ToUpperInvariant().Replace("-", "");
}
