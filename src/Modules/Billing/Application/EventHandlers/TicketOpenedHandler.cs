namespace NeoParking.Billing.Application.EventHandlers;

using MediatR;
using NeoParking.Billing.Application;
using NeoParking.Shared.Kernel.Events;

/// <summary>
/// Handler cross-BC: Billing reage à abertura de um ticket no Access, iniciando o
/// acompanhamento da cobrança. Consome o contrato de integração do Shared Kernel — sem
/// dependência direta do Access BC.
/// </summary>
public sealed class TicketOpenedHandler : INotificationHandler<TicketOpenedIntegrationEvent>
{
    private readonly TicketService _ticketService;

    public TicketOpenedHandler(TicketService ticketService)
        => _ticketService = ticketService;

    public Task Handle(TicketOpenedIntegrationEvent notification, CancellationToken cancellationToken)
        => _ticketService.HandleTicketOpenedAsync(notification);
}
