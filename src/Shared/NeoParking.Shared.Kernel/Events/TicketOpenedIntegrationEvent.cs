namespace NeoParking.Shared.Kernel.Events;

/// <summary>
/// Evento de integração publicado pelo Access BC quando um Ticket é aberto — ou seja,
/// quando a entrada de um veículo é registrada na cancela. Colocado no Shared Kernel para
/// que outros BCs possam consumi-lo sem depender do Access. O Access publica este contrato
/// no seu outbox; o Billing consome para iniciar o acompanhamento da cobrança.
/// </summary>
public record TicketOpenedIntegrationEvent(
    Guid TicketId,
    string Plate,
    Guid? ClientId,
    DateTime EntryAt,
    Guid CorrelationId) : IDomainEvent;
