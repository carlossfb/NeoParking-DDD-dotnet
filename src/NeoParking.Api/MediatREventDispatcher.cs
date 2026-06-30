namespace NeoParking.Api;

using MediatR;
using NeoParking.Shared.Kernel.Events;

public sealed class MediatREventDispatcher : IEventDispatcher
{
    private readonly IMediator _mediator;

    public MediatREventDispatcher(IMediator mediator)
        => _mediator = mediator;

    public async Task DispatchAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
        => await _mediator.Publish(@event, cancellationToken);
}
