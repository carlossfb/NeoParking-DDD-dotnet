namespace NeoParking.Shared.Kernel.Events;

/// <summary>
/// Evento de integração publicado pelo Access BC quando um Ticket é fechado — ou seja,
/// quando a saída de um veículo é registrada na cancela. Colocado no Shared Kernel para
/// que outros BCs possam consumi-lo sem depender do Access. O Access publica este contrato
/// no seu outbox; o Billing consome para calcular e congelar a Fee correspondente.
/// </summary>
public record TicketClosedIntegrationEvent(
    Guid TicketId,
    string Plate,
    DateTime ExitAt,
    Guid CorrelationId) : IDomainEvent;
