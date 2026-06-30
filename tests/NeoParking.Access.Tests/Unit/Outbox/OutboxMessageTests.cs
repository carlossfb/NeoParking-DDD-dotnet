namespace NeoParking.Access.Tests.Unit.Outbox;

using FluentAssertions;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Outbox;

public class OutboxMessageTests
{
    private record TestEvent(string Name, int Value, Guid CorrelationId) : IDomainEvent;

    [Fact]
    public void Create_ShouldSerializePayloadAndSetType()
    {
        var @event = new TestEvent("client-registered", 42, Guid.NewGuid());

        var message = OutboxMessage.Create(@event);

        message.Id.Should().NotBeEmpty();
        message.Type.Should().Contain(nameof(TestEvent));
        message.Payload.Should().Contain("client-registered");
        message.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        message.IsPublished.Should().BeFalse();
        message.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsPublished_ShouldSetPublishedAt()
    {
        var message = OutboxMessage.Create(new TestEvent("x", 1, Guid.NewGuid()));

        message.MarkAsPublished();

        message.IsPublished.Should().BeTrue();
        message.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void MarkAsPublished_WhenCalledTwice_ShouldNotOverwritePublishedAt()
    {
        var message = OutboxMessage.Create(new TestEvent("x", 1, Guid.NewGuid()));
        message.MarkAsPublished();
        var first = message.PublishedAt;

        message.MarkAsPublished();

        message.PublishedAt.Should().Be(first);
    }
}
