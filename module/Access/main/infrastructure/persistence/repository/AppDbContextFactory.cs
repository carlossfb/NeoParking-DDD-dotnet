using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace main.infrastructure
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            
            // Connection string temporária para migrations
            var connectionString = "Server=localhost;Database=neoparking_access;Uid=root;Pwd=password;AllowPublicKeyRetrieval=true;SslMode=none;";
            
            // Para migrations, usar versão específica do MySQL
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 21));
            
            optionsBuilder.UseMySql(connectionString, serverVersion);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}