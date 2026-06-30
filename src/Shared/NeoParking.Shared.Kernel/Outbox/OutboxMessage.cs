namespace NeoParking.Shared.Kernel.Outbox;

using System.Text.Json;
using NeoParking.Shared.Kernel.Events;

public class OutboxMessage : IOutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Payload { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public bool IsPublished => PublishedAt.HasValue;

    // construtor para EF Core
    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    private OutboxMessage(string type, string payload)
    {
        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
        CreatedAt = DateTime.UtcNow;
    }

    public static OutboxMessage Create<TEvent>(TEvent @event) where TEvent : IDomainEvent
    {
        var type = typeof(TEvent).FullName
            ?? throw new InvalidOperationException($"Could not resolve type name for {typeof(TEvent).Name}.");

        var payload = JsonSerializer.Serialize(@event);
        return new OutboxMessage(type, payload);
    }

    /// <summary>
    /// Deserializa o payload de volta para o IDomainEvent original.
    /// Resolve o tipo pelo FullName registrado em Type.
    /// </summary>
    public IDomainEvent Deserialize()
    {
        var eventType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.FullName == Type)
            ?? throw new InvalidOperationException($"Could not resolve event type '{Type}'. Ensure the assembly is loaded.");

        var result = JsonSerializer.Deserialize(Payload, eventType)
            ?? throw new InvalidOperationException($"Failed to deserialize payload for type '{Type}'.");

        if (result is not IDomainEvent domainEvent)
            throw new InvalidOperationException($"Type '{Type}' does not implement IDomainEvent.");

        return domainEvent;
    }

    public void MarkAsPublished()
    {
        if (IsPublished)
            return;

        PublishedAt = DateTime.UtcNow;
    }
}
