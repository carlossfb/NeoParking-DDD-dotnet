namespace NeoParking.Integration.Tests.Support;

using NeoParking.Shared.Kernel.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NeoParking.Access.Infrastructure;
using NeoParking.Billing.Infrastructure;
using NeoParking.Management.Infrastructure;
using NeoParking.Shared.Kernel.Observability;
using NeoParking.Shared.Kernel.Outbox;

/// <summary>
/// Os três módulos com o registro real de DI, sobre MySQL real. Os hosted services não iniciam:
/// o teste roda cada ciclo do outbox explicitamente, sem depender do intervalo de 10s.
/// </summary>
public sealed class ModuleHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    private ModuleHost(ServiceProvider services) => _services = services;

    public ProbeRecorder Probes => _services.GetRequiredService<ProbeRecorder>();

    public static ModuleHost Create(TestDatabases databases)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Access:ConnectionString"] = databases.AccessConnection,
                ["Management:ConnectionString"] = databases.ManagementConnection,
                ["Billing:ConnectionString"] = databases.BillingConnection,
                [MySqlConnectionStrings.PasswordKey] = MySqlFixture.RootPassword,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICorrelationIdProvider, FixedCorrelationIdProvider>();
        services.AddSingleton<ProbeRecorder>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
            typeof(AccessModule).Assembly,
            typeof(BillingModule).Assembly,
            typeof(ManagementModule).Assembly,
            typeof(ModuleHost).Assembly));
        services.AddAccessModule(configuration);
        services.AddManagementModule(configuration);
        services.AddBillingModule(configuration);

        return new ModuleHost(services.BuildServiceProvider());
    }

    public static async Task<ModuleHost> CreateMigratedAsync(TestDatabases databases)
    {
        var host = Create(databases);
        await host.UseAsync<AccessDbContext>(c => c.Database.MigrateAsync());
        await host.UseAsync<ManagementDbContext>(c => c.Database.MigrateAsync());
        await host.UseAsync<BillingDbContext>(c => c.Database.MigrateAsync());
        return host;
    }

    public async Task UseAsync<TService>(Func<TService, Task> action) where TService : notnull
    {
        await using var scope = _services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task<TResult> UseAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task AddToAccessOutboxAsync(params OutboxMessage[] messages)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        context.OutboxMessages.AddRange(messages);
        await context.SaveChangesAsync();
    }

    public OutboxProcessor<AccessDbContext> AccessOutbox(OutboxRetryPolicy? policy = null) => new(
        _services.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<OutboxProcessor<AccessDbContext>>.Instance,
        "Access",
        policy);

    public async Task UseAsync<TFirst, TSecond>(Func<TFirst, TSecond, Task> action)
        where TFirst : notnull
        where TSecond : notnull
    {
        await using var scope = _services.CreateAsyncScope();
        await action(
            scope.ServiceProvider.GetRequiredService<TFirst>(),
            scope.ServiceProvider.GetRequiredService<TSecond>());
    }

    public async Task UseAsync<TFirst, TSecond, TThird>(Func<TFirst, TSecond, TThird, Task> action)
        where TFirst : notnull
        where TSecond : notnull
        where TThird : notnull
    {
        await using var scope = _services.CreateAsyncScope();
        await action(
            scope.ServiceProvider.GetRequiredService<TFirst>(),
            scope.ServiceProvider.GetRequiredService<TSecond>(),
            scope.ServiceProvider.GetRequiredService<TThird>());
    }

    public Task<OutboxMessage> ReloadFromAccessOutboxAsync(Guid id) =>
        UseAsync<AccessDbContext, OutboxMessage>(c =>
            c.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id));

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    private sealed class FixedCorrelationIdProvider : ICorrelationIdProvider
    {
        private readonly Guid _id = Guid.NewGuid();

        public Guid GetCorrelationId() => _id;
    }
}
