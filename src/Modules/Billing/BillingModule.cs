namespace NeoParking.Billing.Infrastructure;

using NeoParking.Shared.Kernel.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoParking.Billing.Application;
using NeoParking.Shared.Kernel.Outbox;
using Microsoft.Extensions.Logging;

public static class BillingModule
{
    public static IServiceCollection AddBillingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Billing:ConnectionString"]
            ?? throw new InvalidOperationException("Billing:ConnectionString not found");

        connectionString = MySqlConnectionStrings.WithPassword(connectionString, configuration);

        services.AddDbContext<BillingDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<IPricingPolicyRepository, MySqlPricingPolicyRepository>();
        services.AddScoped<ITicketRepository, MySqlTicketRepository>();
        services.AddScoped<IUnitOfWork, BillingUnitOfWork>();

        services.AddScoped<PricingPolicyService>();
        services.AddScoped<TicketService>();

        // Registra como concreto E como interface própria do módulo — cada BC resolve seu próprio
        // outbox (IBillingOutboxRepository vs IAccessOutboxRepository vs IManagementOutboxRepository),
        // então não há mais colisão via IOutboxRepository compartilhada no DI.
        services.AddScoped<OutboxRepository<BillingDbContext>>();
        services.AddScoped<BillingOutboxRepository>();
        services.AddScoped<IBillingOutboxRepository>(sp => sp.GetRequiredService<BillingOutboxRepository>());

        services.AddHostedService<OutboxProcessor<BillingDbContext>>(sp =>
            new OutboxProcessor<BillingDbContext>(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<OutboxProcessor<BillingDbContext>>>(),
                moduleName: "Billing"));

        return services;
    }

    public static void InitializeDatabase(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<BillingDbContext>()
            .Database.Migrate();
    }
}
