namespace NeoParking.Management.Tests.Unit.Application.EventHandlers;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NeoParking.Management.Application.EventHandlers;
using NeoParking.Shared.Kernel.Events;

public class ClientRegisteredHandlerTests
{
    private readonly Mock<ILogger<ClientRegisteredHandler>> _logger = new();
    private readonly ClientRegisteredHandler _handler;

    public ClientRegisteredHandlerTests()
        => _handler = new ClientRegisteredHandler(_logger.Object);

    [Fact]
    public async Task Handle_ShouldCompleteWithoutThrowing()
    {
        var @event = new ClientRegisteredIntegrationEvent(Guid.NewGuid(), "Carlos", Guid.NewGuid());

        var act = async () => await _handler.Handle(@event, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
