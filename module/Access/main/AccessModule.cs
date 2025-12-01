using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Linq;
using main.application.service;
using main.domain.ports;
using main.infrastructure;
using main.infrastructure.persistence.mongo;

namespace main
{
    public enum DatabaseProvider
    {
        MySQL,
        MongoDB
    }

    public static class AccessModule
    {
        /// <summary>
        /// Configura todos os serviços do módulo Access
        /// </summary>
        public static IServiceCollection AddAccessModule(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            var connectionString = configuration["Access:ConnectionString"] 
                ?? throw new InvalidOperationException("Access:ConnectionString not found in configuration");
            
            var providerString = configuration["Access:DatabaseProvider"] ?? "MySQL";
            var provider = Enum.Parse<DatabaseProvider>(providerString, ignoreCase: true);

            switch (provider)
            {
                case DatabaseProvider.MySQL:
                    ConfigureMySql(services, connectionString);
                    break;
                case DatabaseProvider.MongoDB:
                    ConfigureMongoDB(services, connectionString);
                    break;
                default:
                    throw new ArgumentException($"Unsupported database provider: {provider}");
            }

            // Mappers (comum para todos os providers)
            services.AddScoped<IClientMapper, main.infrastructure.util.ClientMapper>();
            
            // Services (comum para todos os providers)
            services.AddScoped<IClientService, ClientServiceImpl>();

            return services;
        }

        /// <summary>
        /// Obtém o provider do banco configurado
        /// </summary>
        public static DatabaseProvider GetDatabaseProvider(IConfiguration configuration)
        {
            var providerString = configuration["Access:DatabaseProvider"] ?? "MySQL";
            return Enum.Parse<DatabaseProvider>(providerString, ignoreCase: true);
        }

        private static void ConfigureMySql(IServiceCollection services, string connectionString)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

            services.AddScoped<IClientRepository, MysqlRepositoryImpl>();
        }

        private static void ConfigureMongoDB(IServiceCollection services, string connectionString)
        {
            services.AddSingleton(provider => new MongoDbContext(connectionString));
            services.AddScoped<IClientRepository, MongoRepositoryImpl>();
        }

        /// <summary>
        /// Inicializa o banco de dados
        /// </summary>
        public static void InitializeDatabase(IServiceProvider serviceProvider, DatabaseProvider provider)
        {
            switch (provider)
            {
                case DatabaseProvider.MySQL:
                    InitializeMySql(serviceProvider);
                    break;
                case DatabaseProvider.MongoDB:
                    InitializeMongoDB(serviceProvider);
                    break;
                default:
                    throw new ArgumentException($"Unsupported database provider: {provider}");
            }
        }

        private static void InitializeMySql(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.Migrate();
        }

        private static void InitializeMongoDB(IServiceProvider serviceProvider)
        {
            // MongoDB não precisa de migrations
            // Collections são criadas automaticamente quando inserimos o primeiro documento
            // Opcionalmente, podemos criar índices aqui se necessário
        }
    }
}