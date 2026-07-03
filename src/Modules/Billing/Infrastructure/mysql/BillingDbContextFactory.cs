namespace NeoParking.Billing.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Usado pelo EF Core tooling (dotnet ef migrations add) fora do host.
/// Lê a connection string de uma variável de ambiente para não expor credenciais.
/// </summary>
public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BILLING_CONNECTION_STRING")
            ?? "Server=localhost;Database=neoparking_billing;Uid=root;Pwd=password;";

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        return new BillingDbContext(options);
    }
}
