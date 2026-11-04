namespace NeoParking.Shared.Kernel.Configuration;

using Microsoft.Extensions.Configuration;
using MySqlConnector;

/// <summary>
/// Connection strings versionadas não têm senha: ela vem só de MYSQL_PASSWORD e é aplicada aqui.
/// </summary>
public static class MySqlConnectionStrings
{
    public const string PasswordKey = "MYSQL_PASSWORD";

    public static string WithPassword(string connectionString, IConfiguration configuration) =>
        WithPassword(connectionString, configuration[PasswordKey]);

    public static string WithPassword(string connectionString, string? password)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The MySQL connection string is empty.");

        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException(
                $"{PasswordKey} is not set. Define it in the environment or in .env (see .env.example).");

        return new MySqlConnectionStringBuilder(connectionString) { Password = password }.ConnectionString;
    }

    /// <summary>Para os IDesignTimeDbContextFactory: lê o .env e usa o MySQL local como padrão.</summary>
    public static string ForDesignTime(string connectionStringVariable, string database)
    {
        LocalEnvFile.Load();

        var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("MYSQL_EXPOSED_PORT") ?? "3306";
        var connectionString = Environment.GetEnvironmentVariable(connectionStringVariable)
            ?? $"Server={host};Port={port};Database={database};Uid=neoparking_app;AllowPublicKeyRetrieval=true;SslMode=none;";

        return WithPassword(connectionString, Environment.GetEnvironmentVariable(PasswordKey));
    }
}
