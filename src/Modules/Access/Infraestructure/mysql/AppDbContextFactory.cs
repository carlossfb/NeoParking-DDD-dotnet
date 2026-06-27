namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AccessDbContext>();

        var connectionString = "Server=localhost;Database=neoparking_access;Uid=root;Pwd=password;AllowPublicKeyRetrieval=true;SslMode=none;";
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 21));

        optionsBuilder.UseMySql(connectionString, serverVersion);

        return new AccessDbContext(optionsBuilder.Options);
    }
}