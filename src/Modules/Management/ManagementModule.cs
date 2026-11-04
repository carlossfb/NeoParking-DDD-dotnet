namespace NeoParking.Management.Infrastructure;

using NeoParking.Shared.Kernel.Configuration;
using NeoParking.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoParking.Management.Application;

public static class ManagementModule
{
    public static IServiceCollection AddManagementModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Management:ConnectionString"]
            ?? configuration["Access:ConnectionString"]  // fallback: mesmo servidor do Access
            ?? throw new InvalidOperationException("Management:ConnectionString not found");

        connectionString = MySqlConnectionStrings.WithPassword(connectionString, configuration);

        services.AddDbContext<ManagementDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)).WithoutDatabaseErrorValuesInLogs());

        services.AddScoped<IOperatorRepository, MySqlOperatorRepository>();
        services.AddScoped<IUnitOfWork, ManagementUnitOfWork>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<OperatorService>();

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
