namespace NeoParking.Billing.Application;

// ── requests ─────────────────────────────────────────────────────────────────

/// <summary>
/// Requisição do Owner para definir (ou redefinir) a tarifação: taxa mínima + valor por hora.
/// MinimumFeeCoverageMinutes é quantos minutos a taxa mínima já cobre (default: 60 = 1h).
/// GracePeriodMinutes é a cortesia inicial, totalmente gratuita (default: 0).
/// O Owner vem do token autenticado, nunca do corpo da requisição.
/// </summary>
public sealed record DefinePricingPolicyRequest(
    decimal MinimumFee,
    decimal HourlyRate,
    int MinimumFeeCoverageMinutes = 60,
    int GracePeriodMinutes = 0);

// ── responses ────────────────────────────────────────────────────────────────

public sealed record PricingPolicyResponseDTO(
    Guid Id,
    Guid OwnerId,
    decimal MinimumFee,
    decimal HourlyRate,
    int MinimumFeeCoverageMinutes,
    int GracePeriodMinutes,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive);

public sealed record FeeResponseDTO(
    Guid PricingPolicyId,
    decimal MinimumFee,
    decimal HourlyRate,
    int BilledHours,
    decimal Amount,
    DateTime CalculatedAt);

public sealed record TicketResponseDTO(
    Guid Id,
    string Plate,
    DateTime EntryAt,
    DateTime? ExitAt,
    string Status,
    FeeResponseDTO? Fee);
