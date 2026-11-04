namespace NeoParking.Access.Infrastructure;

using NeoParking.Shared.Kernel.Configuration;
using NeoParking.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Outbox;
using Microsoft.Extensions.Logging;

public static class AccessModule
{
    public static IServiceCollection AddAccessModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Access:ConnectionString"]
            ?? throw new InvalidOperationException("Access:ConnectionString not found");

        connectionString = MySqlConnectionStrings.WithPassword(connectionString, configuration);

        services.AddDbContext<AccessDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)).WithoutDatabaseErrorValuesInLogs());
        services.AddScoped<IClientRepository, MySqlClientRepository>();
        services.AddScoped<ITicketRepository, MySqlTicketRepository>();

        services.AddScoped<OutboxRepository<AccessDbContext>>();
        services.AddScoped<AccessOutboxRepository>();
        services.AddScoped<IAccessOutboxRepository>(sp => sp.GetRequiredService<AccessOutboxRepository>());

        services.AddScoped<IUnitOfWork, AccessUnitOfWork>();

        services.AddScoped<ITicketService, TicketService>();

        services.AddHostedService<OutboxProcessor<AccessDbContext>>(sp =>
            new OutboxProcessor<AccessDbContext>(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<OutboxProcessor<AccessDbContext>>>(),
                moduleName: "Access"));

        services.AddScoped<IClientService, ClientService>();

        return services;
    }

    public static void InitializeDatabase(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.Migrate();
    }
}
