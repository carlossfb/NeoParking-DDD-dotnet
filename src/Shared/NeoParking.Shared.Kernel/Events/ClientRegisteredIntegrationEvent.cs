namespace NeoParking.Shared.Kernel.Events;

/// <summary>
/// Evento de integração publicado pelo Access BC quando um cliente é registrado.
/// Colocado no Shared Kernel para que outros BCs possam consumi-lo sem depender do Access.
/// O Access publica este contrato no outbox; o Management (e outros BCs) consomem.
/// </summary>
public record ClientRegisteredIntegrationEvent(
    Guid ClientId,
    string Name,
    Guid CorrelationId) : IDomainEvent;
