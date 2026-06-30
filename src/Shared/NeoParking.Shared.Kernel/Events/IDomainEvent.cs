namespace NeoParking.Shared.Kernel.Events;

using MediatR;

/// <summary>
/// Marcador para eventos de domínio publicados via outbox.
/// CorrelationId amarra todos os eventos de uma mesma operação de negócio.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid CorrelationId { get; }
}
