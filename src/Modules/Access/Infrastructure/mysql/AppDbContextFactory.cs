namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NeoParking.Shared.Kernel.Configuration;

public sealed class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args)
    {
        var connectionString = MySqlConnectionStrings.ForDesignTime("Access__ConnectionString", "neoparking_access");
        var serverVersion = new MySqlServerVersion(new Version(8, 4, 0));

        var optionsBuilder = new DbContextOptionsBuilder<AccessDbContext>();
        optionsBuilder.UseMySql(connectionString, serverVersion);

        return new AccessDbContext(
            optionsBuilder.Options,
            new AesGcmDocumentProtector(DocumentProtectionKeys.ForDesignTime()));
    }
}
