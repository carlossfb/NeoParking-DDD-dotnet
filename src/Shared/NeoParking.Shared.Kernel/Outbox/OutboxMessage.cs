namespace NeoParking.Shared.Kernel.Outbox;

using System.Text.Json;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;

public class OutboxMessage : IOutboxMessage
{
    public const int LastErrorMaxLength = 2000;

    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Payload { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public bool IsPublished => PublishedAt.HasValue;
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? DeadLetteredAt { get; private set; }

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
        NextAttemptAt = null;
    }

    /// <summary>
    /// Agenda a próxima tentativa ou, esgotado o limite, manda a mensagem para dead-letter.
    /// <see cref="RetryLaterException"/> nunca esgota: a mensagem espera a condição externa mudar.
    /// </summary>
    public void RegisterFailure(Exception error, OutboxRetryPolicy policy, DateTime now)
    {
        AttemptCount++;

        var description = $"{error.GetType().Name}: {error.Message}";
        LastError = description.Length <= LastErrorMaxLength ? description : description[..LastErrorMaxLength];

        if (error is not RetryLaterException && AttemptCount >= policy.MaxAttempts)
        {
            DeadLetteredAt = now;
            NextAttemptAt = null;
            return;
        }

        NextAttemptAt = now + policy.DelayFor(AttemptCount);
    }
}
