namespace NeoParking.Management.Application.EventHandlers;

using MediatR;
using Microsoft.Extensions.Logging;
using NeoParking.Shared.Kernel.Events;

/// <summary>
/// Handler cross-BC: Management reage ao cadastro de um cliente no Access.
/// Consome o contrato de integração do Shared Kernel — sem dependência direta do Access BC.
/// </summary>
public sealed class ClientRegisteredHandler : INotificationHandler<ClientRegisteredIntegrationEvent>
{
    private readonly ILogger<ClientRegisteredHandler> _logger;

    public ClientRegisteredHandler(ILogger<ClientRegisteredHandler> logger)
        => _logger = logger;

    public Task Handle(ClientRegisteredIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[Management] ClientRegistered — Id: {ClientId}, Name: {Name}, CorrelationId: {CorrelationId}.",
            notification.ClientId,
            notification.Name,
            notification.CorrelationId);

        // TODO: lógica real, ex: criar registro de auditoria, verificar elegibilidade para assinatura, etc.

        return Task.CompletedTask;
    }
}
