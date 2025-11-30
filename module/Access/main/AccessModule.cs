using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using main.application.service;
using main.domain.ports;
using main.infrastructure;

namespace main
{
    public static class AccessModule
    {
        /// <summary>
        /// Configura todos os serviços do módulo Access
        /// </summary>
        public static IServiceCollection AddAccessModule(this IServiceCollection services, string connectionString)
        {
            
            services.AddDbContext<AppDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

            // Repositories
            services.AddScoped<IClientRepository, ClientRepositoryImpl>();

            // Services  
            services.AddScoped<IClientService, ClientServiceImpl>();

            return services;
        }

        /// <summary>
        /// Inicializa o banco de dados aplicando as migrations
        /// </summary>
        public static void InitializeDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Database.Migrate();
        }


    }
}