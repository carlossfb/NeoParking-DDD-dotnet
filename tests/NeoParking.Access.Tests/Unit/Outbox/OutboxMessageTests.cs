namespace NeoParking.Access.Tests.Unit.Outbox;

using FluentAssertions;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
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

    private static readonly OutboxRetryPolicy Policy = new(3, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 30)]
    [InlineData(4, 30)]
    [InlineData(2000, 30)]
    public void DelayFor_ShouldGrowExponentiallyUpToTheCap(int attempt, int expectedSeconds)
    {
        Policy.DelayFor(attempt).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void RegisterFailure_BelowTheLimit_ShouldScheduleTheNextAttempt()
    {
        var message = OutboxMessage.Create(new TestEvent("x", 1, Guid.NewGuid()));
        var now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        message.RegisterFailure(new InvalidOperationException("boom"), Policy, now);

        message.AttemptCount.Should().Be(1);
        message.LastError.Should().Be("InvalidOperationException: boom");
        message.NextAttemptAt.Should().Be(now.AddSeconds(10));
        message.DeadLetteredAt.Should().BeNull();
    }

    [Fact]
    public void RegisterFailure_AtTheLimit_ShouldDeadLetter()
    {
        var message = OutboxMessage.Create(new TestEvent("x", 1, Guid.NewGuid()));
        var now = DateTime.UtcNow;

        for (var i = 0; i < Policy.MaxAttempts; i++)
            message.RegisterFailure(new InvalidOperationException("boom"), Policy, now);

        message.DeadLetteredAt.Should().Be(now);
        message.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void RegisterFailure_WithRetryLater_ShouldNeverDeadLetter()
    {
        var message = OutboxMessage.Create(new TestEvent("x", 1, Guid.NewGuid()));
        var now = DateTime.UtcNow;

        for (var i = 0; i < Policy.MaxAttempts * 3; i++)
            message.RegisterFailure(new RetryLaterException("waiting"), Policy, now);

        message.DeadLetteredAt.Should().BeNull();
        message.NextAttemptAt.Should().Be(now.Add(Policy.MaxDelay));
    }

    [Fact]
    public void RegisterFailure_ShouldTruncateLongErrors()
    {
        var message = OutboxMessage.Create(new TestEvent("x", 1, Guid.NewGuid()));

        message.RegisterFailure(new InvalidOperationException(new string('x', 5000)), Policy, DateTime.UtcNow);

        message.LastError.Should().HaveLength(OutboxMessage.LastErrorMaxLength);
    }
}
