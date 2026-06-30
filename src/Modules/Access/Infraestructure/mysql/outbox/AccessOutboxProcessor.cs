namespace NeoParking.Access.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Outbox;

public sealed class AccessOutboxProcessor : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccessOutboxProcessor> _logger;

    public AccessOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<AccessOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Access outbox processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingMessagesAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // Resolve o tipo concreto para não pegar o IOutboxRepository do Management por engano
        var outbox     = scope.ServiceProvider.GetRequiredService<AccessOutboxRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();

        var pending = await outbox.GetPendingAsync(batchSize: 50);

        if (pending.Count == 0)
            return;

        _logger.LogInformation("Access outbox: processing {Count} pending message(s).", pending.Count);

        var published = new List<OutboxMessage>();

        foreach (var message in pending)
        {
            try
            {
                var domainEvent = message.Deserialize();
                await dispatcher.DispatchAsync(domainEvent, cancellationToken);

                published.Add(message);

                _logger.LogDebug(
                    "Dispatched {Type} ({Id}) correlationId: {CorrelationId}.",
                    message.Type, message.Id, domainEvent.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to dispatch {Type} ({Id}). Will retry on next cycle.", message.Type, message.Id);
                break;
            }
        }

        if (published.Count > 0)
            await outbox.MarkAsPublishedAsync(published);
    }
}
