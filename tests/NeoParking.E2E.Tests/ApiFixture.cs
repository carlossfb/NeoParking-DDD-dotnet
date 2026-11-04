namespace NeoParking.E2E.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.MySql;

/// <summary>
/// Sobe a API real contra um MySQL em container, com os três bancos de scripts/mysql/init.sql.
/// As migrations reais rodam no boot da aplicação (Program.cs), com o usuário restrito da aplicação.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DbPassword = "e2e-test-only";
    private const string AppUser = "neoparking_app";

    // Mesma imagem do docker-compose.yml.
    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4")
        .WithUsername("root")
        .WithPassword(DbPassword)
        .Build();

    // Gerado a cada execução: nenhum segredo fica versionado.
    private readonly string _jwtSecret = $"{Guid.NewGuid():N}{Guid.NewGuid():N}";

    public LogCapture Logs { get; } = new();

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();

        var script = await File.ReadAllTextAsync(FindMySqlScript("init.sql"))
            + $"\nCREATE USER '{AppUser}'@'%' IDENTIFIED BY '{DbPassword}';\n"
            + await File.ReadAllTextAsync(FindMySqlScript("app-grants.sql"));

        var result = await _mysql.ExecScriptAsync(script);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"MySQL setup failed: {result.Stderr}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting chega ao Program antes de builder.Build(), que é quando os módulos leem a configuração.
        builder.UseSetting("Access:ConnectionString", ConnectionString("neoparking_access"));
        builder.UseSetting("Management:ConnectionString", ConnectionString("neoparking_management"));
        builder.UseSetting("Billing:ConnectionString", ConnectionString("neoparking_billing"));
        builder.UseSetting("MYSQL_PASSWORD", DbPassword);
        builder.UseSetting("Jwt:Secret", _jwtSecret);
        builder.ConfigureLogging(logging => logging.Services.AddSingleton<ILoggerProvider>(Logs));
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _mysql.DisposeAsync();
    }

    private string ConnectionString(string database) =>
        $"Server={_mysql.Hostname};Port={_mysql.GetMappedPublicPort(MySqlBuilder.MySqlPort)};" +
        $"Database={database};Uid={AppUser};AllowPublicKeyRetrieval=true;SslMode=none;";

    private static string FindMySqlScript(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var script = Path.Combine(dir.FullName, "scripts", "mysql", name);
            if (File.Exists(script))
                return script;
        }

        throw new FileNotFoundException($"scripts/mysql/{name} not found above the test output directory.");
    }
}
