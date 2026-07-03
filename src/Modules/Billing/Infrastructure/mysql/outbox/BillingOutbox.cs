namespace NeoParking.Billing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeoParking.Billing.Application;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Outbox;

public sealed class BillingOutboxRepository : IBillingOutboxRepository
{
    private readonly BillingDbContext _context;

    public BillingOutboxRepository(BillingDbContext context) => _context = context;

    public async Task AddAsync(OutboxMessage message)
        // Sem SaveChangesAsync — coordenado pelo IUnitOfWork junto com o resto da transação.
        => await _context.OutboxMessages.AddAsync(message);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize = 50)
        => await _context.OutboxMessages
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync();

    public async Task MarkAsPublishedAsync(IEnumerable<OutboxMessage> messages)
    {
        foreach (var m in messages)
            m.MarkAsPublished();

        await _context.SaveChangesAsync();
    }
}

public sealed class BillingOutboxProcessor : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingOutboxProcessor> _logger;

    public BillingOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<BillingOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Billing outbox processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // Resolve o tipo concreto para não pegar o IOutboxRepository de outro BC por engano.
        var outbox     = scope.ServiceProvider.GetRequiredService<BillingOutboxRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();

        var pending = await outbox.GetPendingAsync();
        if (pending.Count == 0) return;

        _logger.LogInformation("Billing outbox: processing {Count} message(s).", pending.Count);

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
