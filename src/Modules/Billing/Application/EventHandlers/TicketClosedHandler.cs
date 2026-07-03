namespace NeoParking.Billing.Application.EventHandlers;

using MediatR;
using NeoParking.Billing.Application;
using NeoParking.Shared.Kernel.Events;

/// <summary>
/// Handler cross-BC: Billing reage ao fechamento de um ticket no Access, calculando e
/// congelando a Fee com a PricingPolicy vigente na saída. Consome o contrato de integração
/// do Shared Kernel — sem dependência direta do Access BC.
/// </summary>
public sealed class TicketClosedHandler : INotificationHandler<TicketClosedIntegrationEvent>
{
    private readonly TicketService _ticketService;

    public TicketClosedHandler(TicketService ticketService)
        => _ticketService = ticketService;

    public Task Handle(TicketClosedIntegrationEvent notification, CancellationToken cancellationToken)
        => _ticketService.HandleTicketClosedAsync(notification);
}
