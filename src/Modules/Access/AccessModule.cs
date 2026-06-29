namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Outbox;

public enum DatabaseProvider { MySQL, MongoDB }

public static class AccessModule
{
    public static IServiceCollection AddAccessModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Access:ConnectionString"]
            ?? throw new InvalidOperationException("Access:ConnectionString not found");

        var provider = Enum.Parse<DatabaseProvider>(
            configuration["Access:DatabaseProvider"] ?? "MySQL", ignoreCase: true);

        switch (provider)
        {
            case DatabaseProvider.MySQL:
                services.AddDbContext<AccessDbContext>(options =>
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
                services.AddScoped<IClientRepository, MySqlClientRepository>();

                services.AddScoped<AccessOutboxRepository>();
                services.AddScoped<IOutboxRepository>(sp => sp.GetRequiredService<AccessOutboxRepository>());

                services.AddScoped<IUnitOfWork, AccessUnitOfWork>();

                services.AddHostedService<AccessOutboxProcessor>();
                break;
            case DatabaseProvider.MongoDB:
                services.AddSingleton(_ => new MongoDbContext(connectionString));
                services.AddScoped<IClientRepository, MongoClientRepository>();
                // outbox não suportado no provider MongoDB por ora
                break;
            default:
                throw new ArgumentException($"Unsupported provider: {provider}");
        }

        services.AddScoped<IClientService, ClientService>();

        return services;
    }

    public static void InitializeDatabase(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var provider = Enum.Parse<DatabaseProvider>(
            configuration["Access:DatabaseProvider"] ?? "MySQL", ignoreCase: true);

        if (provider == DatabaseProvider.MySQL)
        {
            using var scope = serviceProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AccessDbContext>().Database.Migrate();
        }
    }
}
