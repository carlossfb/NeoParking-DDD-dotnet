namespace NeoParking.Billing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NeoParking.Shared.Kernel.Configuration;

/// <summary>
/// Usado pelo EF Core tooling (dotnet ef migrations add) fora do host.
/// Lê a connection string de uma variável de ambiente para não expor credenciais.
/// </summary>
public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var connectionString = MySqlConnectionStrings.ForDesignTime("BILLING_CONNECTION_STRING", "neoparking_billing");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        return new BillingDbContext(options);
    }
}
