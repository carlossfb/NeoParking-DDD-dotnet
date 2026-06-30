namespace NeoParking.Management.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoParking.Management.Application;
using NeoParking.Shared.Kernel.Outbox;

public static class ManagementModule
{
    public static IServiceCollection AddManagementModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Management:ConnectionString"]
            ?? configuration["Access:ConnectionString"]  // fallback: mesmo servidor do Access
            ?? throw new InvalidOperationException("Management:ConnectionString not found");

        services.AddDbContext<ManagementDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddScoped<IOperatorRepository, MySqlOperatorRepository>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<OperatorService>();

        // Registra como concreto E como interface — o AccessOutboxProcessor resolve AccessOutboxRepository,
        // o ManagementOutboxProcessor resolve ManagementOutboxRepository; sem colisão no DI
        services.AddScoped<ManagementOutboxRepository>();
        services.AddScoped<IOutboxRepository>(sp => sp.GetRequiredService<ManagementOutboxRepository>());

        services.AddHostedService<ManagementOutboxProcessor>();

        return services;
    }

    public static void InitializeDatabase(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<ManagementDbContext>()
            .Database.Migrate();
    }
}
