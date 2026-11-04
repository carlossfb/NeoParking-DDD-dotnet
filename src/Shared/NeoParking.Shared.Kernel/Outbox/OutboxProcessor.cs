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
    private readonly OutboxRetryPolicy _retryPolicy;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor<TContext>> logger,
        string moduleName,
        OutboxRetryPolicy? retryPolicy = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _moduleName = moduleName;
        _retryPolicy = retryPolicy ?? OutboxRetryPolicy.Default;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Module} outbox processor started.", _moduleName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Banco fora do ar, por exemplo. Uma exceção aqui encerraria o host inteiro.
                _logger.LogError(ex, "{Module} outbox cycle failed. Will retry.", _moduleName);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>
    /// Um ciclo do outbox. Cada mensagem é entregue em um escopo próprio e tem o resultado salvo
    /// em seguida: uma escrita que falhou em um handler não contamina a mensagem seguinte, e uma
    /// queda no meio do lote não repete as entregas já registradas.
    /// </summary>
    public async Task ProcessOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();

        // Resolve o repositório concreto do módulo (ex.: AccessOutboxRepository), não a interface
        // compartilhada IOutboxRepository, para não pegar o outbox de outro BC por engano.
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxRepository<TContext>>();

        var pending = await outbox.GetPendingAsync();
        if (pending.Count == 0)
            return;

        _logger.LogInformation("{Module} outbox: processing {Count} message(s).", _moduleName, pending.Count);

        foreach (var message in pending)
        {
            try
            {
                var domainEvent = message.Deserialize();

                using var deliveryScope = _scopeFactory.CreateScope();
                var publisher = deliveryScope.ServiceProvider.GetRequiredService<IPublisher>();
                await publisher.Publish(domainEvent, cancellationToken);

                message.MarkAsPublished();

                _logger.LogDebug(
                    "Dispatched {Type} ({Id}) correlationId: {CorrelationId}.",
                    message.Type, message.Id, domainEvent.CorrelationId);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Sem break: uma mensagem com erro não pode travar as seguintes do lote. O backoff
                // tira a mensagem dos próximos ciclos, então ela também não ocupa o lote.
                message.RegisterFailure(ex, _retryPolicy, DateTime.UtcNow);

                if (message.DeadLetteredAt is null)
                    _logger.LogWarning(ex,
                        "Failed to dispatch {Type} ({Id}), attempt {Attempt}. Next attempt at {NextAttemptAt:O}.",
                        message.Type, message.Id, message.AttemptCount, message.NextAttemptAt);
                else
                    _logger.LogError(ex,
                        "Dead-lettered {Type} ({Id}) after {Attempt} attempts. See scripts/mysql/outbox-replay.sql.",
                        message.Type, message.Id, message.AttemptCount);
            }

            await outbox.SaveAsync(cancellationToken);
        }
    }
}
