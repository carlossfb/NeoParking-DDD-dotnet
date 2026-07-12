namespace NeoParking.Shared.Kernel.Outbox;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// BackgroundService genérico que faz polling do outbox de um módulo e publica os eventos
/// pendentes via MediatR IPublisher. Cada módulo registra uma instância parametrizada pelo seu
/// próprio DbContext, resolvendo o repositório concreto do módulo (ex.: AccessOutboxRepository)
/// para não pegar o outbox de outro bounded context por engano.
/// </summary>
public sealed class OutboxProcessor<TContext> : BackgroundService where TContext : DbContext
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor<TContext>> _logger;
    private readonly string _moduleName;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor<TContext>> logger,
        string moduleName)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _moduleName = moduleName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Module} outbox processor started.", _moduleName);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // Resolve o repositório concreto do módulo (ex.: AccessOutboxRepository), não a interface
        // compartilhada IOutboxRepository, para não pegar o outbox de outro BC por engano.
        var outbox    = scope.ServiceProvider.GetRequiredService<OutboxRepository<TContext>>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var pending = await outbox.GetPendingAsync();
        if (pending.Count == 0)
            return;

        _logger.LogInformation("{Module} outbox: processing {Count} message(s).", _moduleName, pending.Count);

        var published = new List<OutboxMessage>();

        foreach (var message in pending)
        {
            try
            {
                var domainEvent = message.Deserialize();
                await publisher.Publish(domainEvent, cancellationToken);

                published.Add(message);

                _logger.LogDebug(
                    "Dispatched {Type} ({Id}) correlationId: {CorrelationId}.",
                    message.Type, message.Id, domainEvent.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to dispatch {Type} ({Id}). Will retry.", message.Type, message.Id);

                // Não damos break aqui: uma mensagem "poison" (ex.: TicketClosed sem pricing policy
                // vigente naquele instante) não pode travar as mensagens seguintes do lote pra sempre.
                // Ela continua sendo reprocessada a cada ciclo (não é marcada como publicada), mas as
                // outras seguem seu fluxo normal.
                continue;
            }
        }

        if (published.Count > 0)
            await outbox.MarkAsPublishedAsync(published);
    }
}
