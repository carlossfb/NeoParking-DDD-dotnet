namespace NeoParking.Integration.Tests.Support;

using System.Collections.Concurrent;
using MediatR;
using NeoParking.Billing.Domain;
using NeoParking.Billing.Infrastructure;
using NeoParking.Shared.Kernel.Events;

/// <summary>Evento de teste: o handler falha enquanto o nome dele estiver em <see cref="ProbeRecorder.Failing"/>.</summary>
public sealed record ProbeEvent(string Name, Guid CorrelationId) : IDomainEvent;

/// <summary>Evento de teste cujo handler escreve no DbContext do Billing, igual aos handlers reais.</summary>
public sealed record DirtyWriteEvent(string Plate, Guid CorrelationId) : IDomainEvent;

public sealed class ProbeRecorder
{
    public ConcurrentDictionary<string, byte> Failing { get; } = new();
    public ConcurrentQueue<string> Delivered { get; } = new();
    public ConcurrentQueue<string> Attempted { get; } = new();
}

public sealed class ProbeHandler : INotificationHandler<ProbeEvent>
{
    private readonly ProbeRecorder _recorder;

    public ProbeHandler(ProbeRecorder recorder) => _recorder = recorder;

    public Task Handle(ProbeEvent notification, CancellationToken cancellationToken)
    {
        _recorder.Attempted.Enqueue(notification.Name);

        if (_recorder.Failing.ContainsKey(notification.Name))
            throw new InvalidOperationException($"Probe '{notification.Name}' failed.");

        _recorder.Delivered.Enqueue(notification.Name);
        return Task.CompletedTask;
    }
}

public sealed class DirtyWriteHandler : INotificationHandler<DirtyWriteEvent>
{
    private readonly BillingDbContext _billing;

    public DirtyWriteHandler(BillingDbContext billing) => _billing = billing;

    public async Task Handle(DirtyWriteEvent notification, CancellationToken cancellationToken)
    {
        _billing.Tickets.Add(Ticket.OpenFromAccess(Guid.NewGuid(), notification.Plate, DateTime.UtcNow));
        await _billing.SaveChangesAsync(cancellationToken);
    }
}
