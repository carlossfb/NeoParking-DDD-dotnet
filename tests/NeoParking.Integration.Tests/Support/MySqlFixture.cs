namespace NeoParking.Integration.Tests.Support;

using MySqlConnector;
using Testcontainers.MySql;

[CollectionDefinition(Name)]
public sealed class MySqlCollection : ICollectionFixture<MySqlFixture>
{
    public const string Name = "MySql";
}

/// <summary>
/// Um container MySQL 8.4 (a imagem do compose) por execução. Cada teste pede schemas próprios,
/// então pode semear e migrar sem interferir nos outros.
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    public const string RootPassword = "integration-test-only";

    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithUsername("root")
        .WithPassword(RootPassword)
        .Build();

    public Task InitializeAsync() => _mysql.StartAsync();

    public Task DisposeAsync() => _mysql.DisposeAsync().AsTask();

    public async Task<TestDatabases> CreateDatabasesAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var databases = new TestDatabases(
            $"access_{suffix}", $"management_{suffix}", $"billing_{suffix}", ConnectionString);

        await ExecuteAsync(
            $"CREATE DATABASE {databases.Access} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; " +
            $"CREATE DATABASE {databases.Management} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; " +
            $"CREATE DATABASE {databases.Billing} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");

        return databases;
    }

    public async Task<MySqlConnection> OpenAsync()
    {
        var connection = new MySqlConnection(ConnectionString(""));
        await connection.OpenAsync();
        return connection;
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private string ConnectionString(string database) =>
        $"Server={_mysql.Hostname};Port={_mysql.GetMappedPublicPort(MySqlBuilder.MySqlPort)};" +
        $"Database={database};Uid=root;Pwd={RootPassword};AllowPublicKeyRetrieval=true;SslMode=none;";
}

public sealed record TestDatabases(
    string Access, string Management, string Billing, Func<string, string> ConnectionStringFor)
{
    public string AccessConnection => ConnectionStringFor(Access);
    public string ManagementConnection => ConnectionStringFor(Management);
    public string BillingConnection => ConnectionStringFor(Billing);
}
