namespace NeoParking.Shared.Kernel.Persistence;

using MySqlConnector;

/// <summary>Cada módulo registra o seu, e a API verifica todos sem conhecer os DbContexts.</summary>
public interface IDatabaseProbe
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Conecta direto em vez de resolver o DbContext: o AddDbContext dos módulos chama
/// ServerVersion.AutoDetect ao criar as opções, de forma síncrona, o que lançaria fora do health
/// check com o banco fora do ar e ignoraria o timeout.
/// </summary>
public sealed class MySqlDatabaseProbe(string connectionString) : IDatabaseProbe
{
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.PingAsync(cancellationToken);
    }
}
