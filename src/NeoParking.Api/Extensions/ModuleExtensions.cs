namespace NeoParking.Api.Extensions;

using NeoParking.Access.Infrastructure;
using NeoParking.Management.Infrastructure;
using NeoParking.Billing.Infrastructure; 

public static class ModuleExtensions
{
    /// <summary>
    /// Ponto único de registro de todos os Bounded Contexts na composição da API.
    /// Ao criar um novo BC, adicione a chamada AddXModule aqui — um lugar só,
    /// em vez de espalhar registros pelo Program.cs.
    /// </summary>
    public static IServiceCollection AddNeoParkingModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAccessModule(configuration);
        services.AddManagementModule(configuration);
        services.AddBillingModule(configuration);

        return services;
    }

    /// <summary>
    /// Aplica as migrations de cada Bounded Context no boot da aplicação.
    /// Só é chamado fora do design-time do EF (ver EF_DESIGN_TIME em Program.cs).
    /// </summary>
    public static void InitializeNeoParkingDatabases(
        this IServiceProvider services,
        IConfiguration configuration)
    {
        AccessModule.InitializeDatabase(services, configuration);
        ManagementModule.InitializeDatabase(services);
        BillingModule.InitializeDatabase(services);
    }
}
