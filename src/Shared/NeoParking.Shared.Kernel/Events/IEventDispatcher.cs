namespace NeoParking.Shared.Kernel.Events;

/// <summary>
/// Despacha eventos de domínio de forma síncrona in-process.
/// Implementado na API usando MediatR — sem broker externo.
/// </summary>
public interface IEventDispatcher
{
    Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken = default);
}
