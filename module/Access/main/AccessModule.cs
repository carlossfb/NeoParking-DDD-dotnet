using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
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
        public static IServiceCollection AddAccessModule(this IServiceCollection services)
        {
            // DbContext - responsabilidade do módulo Access
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("AccessDB"));

            // Repositories
            services.AddScoped<IClientRepository, ClientRepositoryImpl>();

            // Services  
            services.AddScoped<IClientService, ClientServiceImpl>();

            return services;
        }


    }
}