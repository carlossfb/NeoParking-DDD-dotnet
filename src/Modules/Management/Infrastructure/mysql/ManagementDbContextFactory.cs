namespace NeoParking.Management.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Usado pelo EF Core tooling (dotnet ef migrations add) fora do host.
/// Lê a connection string de uma variável de ambiente para não expor credenciais.
/// </summary>
public sealed class ManagementDbContextFactory : IDesignTimeDbContextFactory<ManagementDbContext>
{
    public ManagementDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MANAGEMENT_CONNECTION_STRING")
            ?? "Server=localhost;Database=neoparking_management;Uid=root;Pwd=password;";

        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;

        return new ManagementDbContext(options);
    }
}
