namespace NeoParking.Shared.Kernel.Outbox;

public interface IOutboxMessage
{
    Guid Id { get; }
    string Type { get; }
    string Payload { get; }
    DateTime CreatedAt { get; }
    DateTime? PublishedAt { get; }
    bool IsPublished { get; }
}
