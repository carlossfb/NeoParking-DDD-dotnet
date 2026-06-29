namespace NeoParking.Access.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args)
    {
        LoadEnvFile();

        var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("MYSQL_EXPOSED_PORT") ?? "3306";
        var database = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "neoparking_access";
        var password = Environment.GetEnvironmentVariable("MYSQL_ROOT_PASSWORD") ?? "password";

        var connectionString =
            Environment.GetEnvironmentVariable("Access__ConnectionString")
            ?? $"Server={host};Port={port};Database={database};Uid=root;Pwd={password};AllowPublicKeyRetrieval=true;SslMode=none;";

        var serverVersion = new MySqlServerVersion(new Version(8, 4, 0));

        var optionsBuilder = new DbContextOptionsBuilder<AccessDbContext>();
        optionsBuilder.UseMySql(connectionString, serverVersion);

        return new AccessDbContext(optionsBuilder.Options);
    }

    private static void LoadEnvFile()
    {
        // Sobe na árvore de diretórios até encontrar o .env (útil independente de onde o ef é chamado)
        var directory = Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(directory))
        {
            var envPath = Path.Combine(directory, ".env");

            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    // ignora comentários e linhas vazias
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                        continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Split('#')[0].Trim(); // remove comentário inline

                    // não sobrescreve variável já definida no ambiente
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        Environment.SetEnvironmentVariable(key, value);
                }

                break;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }
    }
}
