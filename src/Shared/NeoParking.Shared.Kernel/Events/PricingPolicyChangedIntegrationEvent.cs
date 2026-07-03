namespace NeoParking.Shared.Kernel.Events;

/// <summary>
/// Evento de integração publicado pelo Billing BC sempre que o Owner define uma nova
/// política de tarifação (taxa mínima + valor por hora). Colocado no Shared Kernel para
/// que outros BCs (ex: notificações, auditoria, relatórios) possam reagir sem depender
/// do Billing. O Billing publica este contrato no seu outbox; outros BCs consomem.
/// </summary>
public record PricingPolicyChangedIntegrationEvent(
    Guid PricingPolicyId,
    Guid OwnerId,
    decimal MinimumFee,
    decimal HourlyRate,
    DateTime EffectiveFrom,
    Guid CorrelationId) : IDomainEvent;
