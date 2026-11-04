namespace NeoParking.Integration.Tests.Migrations;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;
using NeoParking.Access.Application;
using NeoParking.Access.Infrastructure;
using NeoParking.Billing.Infrastructure;
using NeoParking.Integration.Tests.Support;
using NeoParking.Management.Infrastructure;
using NeoParking.Shared.Kernel.Outbox;

/// <summary>Migrations do zero e sobre um banco com dados da versão anterior.</summary>
[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MigrationTests
{
    private const string BeforeUnique = "AddOutboxRetry";
    private const string BeforeTicketCode = "AddUniqueConstraints";
    private const string BeforeClientType = "AddTicketCode";

    private const string Now = "'2026-01-01 00:00:00'";

    private readonly MySqlFixture _mysql;

    public MigrationTests(MySqlFixture mysql) => _mysql = mysql;

    [Fact]
    public async Task FromScratch_ShouldCreateEveryUniqueIndex()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = await ModuleHost.CreateMigratedAsync(databases);

        (await UniqueIndexesAsync(databases.Access)).Should().Contain(
            ["ux_vehicles_plate", "ux_clients_document", "ux_tickets_open_plate"]);
        (await UniqueIndexesAsync(databases.Management)).Should().Contain("ux_management_operators_owner");
        (await UniqueIndexesAsync(databases.Billing)).Should().Contain("ux_billing_pricing_policies_active");
    }

    [Theory]
    [InlineData("vehicles", "duplicate plates in vehicles")]
    [InlineData("clients", "duplicate CPFs in clients")]
    [InlineData("tickets", "plates with several open tickets")]
    public async Task AccessDuplicates_ShouldFailClearly_AndKeepTheData(string rule, string expectedMessage)
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateAccessToAsync(host, BeforeUnique);
        var (seed, table, fix) = AccessSeed(rule, databases.Access);
        await _mysql.ExecuteAsync(seed);
        var rowsBefore = await CountAsync($"{databases.Access}.{table}");

        var act = () => host.UseAsync<AccessDbContext>(c => c.Database.MigrateAsync());

        (await act.Should().ThrowAsync<MySqlException>()).Which.Message.Should()
            .Contain(expectedMessage).And.Contain("scripts/mysql/detect-duplicates.sql");
        (await CountAsync($"{databases.Access}.{table}")).Should().Be(rowsBefore);
        (await UniqueIndexesAsync(databases.Access)).Should().NotContain(
            ["ux_vehicles_plate", "ux_clients_cpf", "ux_tickets_open_plate"]);

        // Depois da correção manual, a mesma migration conclui.
        await _mysql.ExecuteAsync(fix);
        await host.UseAsync<AccessDbContext>(c => c.Database.MigrateAsync());
        (await UniqueIndexesAsync(databases.Access)).Should().Contain("ux_tickets_open_plate");
    }

    [Fact]
    public async Task SecondOwner_ShouldFailClearly_AndKeepTheData()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = ModuleHost.Create(databases);
        await host.UseAsync<ManagementDbContext>(c => c.GetService<IMigrator>().MigrateAsync(BeforeUnique));
        await _mysql.ExecuteAsync(OwnerInsert(databases.Management, "a") + OwnerInsert(databases.Management, "b"));

        var act = () => host.UseAsync<ManagementDbContext>(c => c.Database.MigrateAsync());

        (await act.Should().ThrowAsync<MySqlException>()).Which.Message.Should().Contain("more than one Owner");
        (await CountAsync($"{databases.Management}.management_operators")).Should().Be(2);
        (await UniqueIndexesAsync(databases.Management)).Should().NotContain("ux_management_operators_owner");
    }

    [Fact]
    public async Task SecondActivePolicy_ShouldFailClearly_AndKeepTheData()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = ModuleHost.Create(databases);
        await host.UseAsync<BillingDbContext>(c => c.GetService<IMigrator>().MigrateAsync(BeforeUnique));
        await _mysql.ExecuteAsync(PolicyInsert(databases.Billing) + PolicyInsert(databases.Billing));

        var act = () => host.UseAsync<BillingDbContext>(c => c.Database.MigrateAsync());

        (await act.Should().ThrowAsync<MySqlException>()).Which.Message.Should().Contain("more than one active pricing policy");
        (await CountAsync($"{databases.Billing}.billing_pricing_policies")).Should().Be(2);
        (await UniqueIndexesAsync(databases.Billing)).Should().NotContain("ux_billing_pricing_policies_active");
    }

    [Fact]
    public async Task ExistingDataWithoutDuplicates_ShouldMigrate_AndRemainUntouched()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateAccessToAsync(host, BeforeUnique);
        await host.UseAsync<ManagementDbContext>(c => c.GetService<IMigrator>().MigrateAsync(BeforeUnique));
        await host.UseAsync<BillingDbContext>(c => c.GetService<IMigrator>().MigrateAsync(BeforeUnique));
        await _mysql.ExecuteAsync(
            ClientInsert(databases.Access, "11144477735")
            + TicketInsert(databases.Access, "ABC1234", "Closed")
            + TicketInsert(databases.Access, "ABC1234", "Closed")
            + TicketInsert(databases.Access, "ABC1234", "Open")
            + OwnerInsert(databases.Management, "a")
            + PolicyInsert(databases.Billing));

        await host.UseAsync<AccessDbContext>(c => c.Database.MigrateAsync());
        await host.UseAsync<ManagementDbContext>(c => c.Database.MigrateAsync());
        await host.UseAsync<BillingDbContext>(c => c.Database.MigrateAsync());

        (await CountAsync($"{databases.Access}.tickets")).Should().Be(3);
        (await CountAsync($"{databases.Management}.management_operators")).Should().Be(1);
        (await CountAsync($"{databases.Billing}.billing_pricing_policies")).Should().Be(1);
    }

    [Theory]
    [InlineData("access")]
    [InlineData("billing")]
    public async Task PendingOutboxMessages_FromThePreviousVersion_ShouldRemainEligible(string module)
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = ModuleHost.Create(databases);
        var id = Guid.NewGuid();

        switch (module)
        {
            case "access":
                await MigrateAccessToAsync(host, "AddTickets");
                await _mysql.ExecuteAsync(OutboxInsert($"{databases.Access}.outbox_messages", id));
                await host.UseAsync<AccessDbContext>(c => c.Database.MigrateAsync());
                await PendingShouldContainAsync<AccessDbContext>(host, id);
                break;
            default:
                await host.UseAsync<BillingDbContext>(c => c.GetService<IMigrator>().MigrateAsync("Initial"));
                await _mysql.ExecuteAsync(OutboxInsert($"{databases.Billing}.billing_outbox_messages", id));
                await host.UseAsync<BillingDbContext>(c => c.Database.MigrateAsync());
                await PendingShouldContainAsync<BillingDbContext>(host, id);
                break;
        }
    }

    [Fact]
    public async Task ManagementOutbox_WithRows_ShouldFailClearly_AndKeepTheData()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = ModuleHost.Create(databases);
        var table = $"{databases.Management}.management_outbox_messages";
        await host.UseAsync<ManagementDbContext>(c => c.GetService<IMigrator>().MigrateAsync("AddUniqueConstraints"));
        await _mysql.ExecuteAsync(OutboxInsert(table, Guid.NewGuid()));

        var act = () => host.UseAsync<ManagementDbContext>(c => c.Database.MigrateAsync());

        (await act.Should().ThrowAsync<MySqlException>()).Which.Message.Should().Contain("management_outbox_messages");
        (await CountAsync(table)).Should().Be(1);

        // Depois de esvaziar a tabela à mão, a mesma migration conclui.
        await _mysql.ExecuteAsync($"DELETE FROM {table};");
        await host.UseAsync<ManagementDbContext>(c => c.Database.MigrateAsync());
        (await ReadFirstColumnAsync(TableQuery(databases.Management))).Should().NotContain("management_outbox_messages");
    }

    [Fact]
    public async Task ManagementFromScratch_ShouldHaveNoOutboxTable_AndMatchTheModel()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = await ModuleHost.CreateMigratedAsync(databases);

        (await ReadFirstColumnAsync(TableQuery(databases.Management))).Should().NotContain("management_outbox_messages");
        await host.UseAsync<ManagementDbContext>(c =>
        {
            c.Database.HasPendingModelChanges().Should().BeFalse();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RemovingTheManagementOutbox_ShouldBeReversible()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = await ModuleHost.CreateMigratedAsync(databases);

        await host.UseAsync<ManagementDbContext>(c => c.GetService<IMigrator>().MigrateAsync("AddUniqueConstraints"));

        (await CountAsync($"{databases.Management}.management_outbox_messages")).Should().Be(0);
    }

    [Fact]
    public async Task DetectionScript_ShouldListEveryDuplicate_WithoutChangingAnything()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateAccessToAsync(host, BeforeUnique);
        await host.UseAsync<ManagementDbContext>(c => c.GetService<IMigrator>().MigrateAsync(BeforeUnique));
        await host.UseAsync<BillingDbContext>(c => c.GetService<IMigrator>().MigrateAsync(BeforeUnique));
        await _mysql.ExecuteAsync(
            AccessSeed("vehicles", databases.Access).Seed
            + AccessSeed("clients", databases.Access).Seed
            + AccessSeed("tickets", databases.Access).Seed
            + OwnerInsert(databases.Management, "a") + OwnerInsert(databases.Management, "b")
            + PolicyInsert(databases.Billing) + PolicyInsert(databases.Billing));

        var rules = await ReadFirstColumnAsync(await RepositoryScripts.ReadAsync("detect-duplicates.sql", databases));

        rules.Should().Contain(
        [
            "vehicles.plate", "clients.cpf", "tickets.open_plate",
            "management_operators.owner", "billing_pricing_policies.active"
        ]);
    }

    [Fact]
    public async Task FromScratch_ShouldCreateTheTicketCodeIndex_AndMatchTheAccessModel()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = await ModuleHost.CreateMigratedAsync(databases);

        (await UniqueIndexesAsync(databases.Access)).Should().Contain("ux_tickets_code");
        await host.UseAsync<AccessDbContext>(c =>
        {
            c.Database.HasPendingModelChanges().Should().BeFalse();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ExistingTickets_ShouldReceiveCodes_WithTheRestrictedUser_WithoutChangingEntryAt()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        var restricted = await RestrictedUserAsync(databases);
        await using var host = ModuleHost.Create(restricted);
        await MigrateAccessToAsync(host, BeforeTicketCode);
        await _mysql.ExecuteAsync(
            TicketInsert(databases.Access, "ABC1234", "Open")
            + TicketInsert(databases.Access, "ABC1234", "Closed")
            + TicketInsert(databases.Access, "XYZ9876", "Closed"));

        await host.UseAsync<AccessDbContext>(c => c.Database.MigrateAsync());

        var codes = await ReadFirstColumnAsync($"SELECT code FROM {databases.Access}.tickets");
        codes.Should().HaveCount(3).And.OnlyHaveUniqueItems()
            .And.OnlyContain(c => System.Text.RegularExpressions.Regex.IsMatch(c, "^[0-9A-F]{20}$"));
        (await CountAsync($"{databases.Access}.tickets WHERE entry_at = {Now}")).Should().Be(3);
        (await CountAsync($"{databases.Access}.tickets WHERE exit_method IS NULL")).Should().Be(3);

        // O ticket aberto antes da migração sai pelo código recebido.
        var openCode = (await ReadFirstColumnAsync(
            $"SELECT code FROM {databases.Access}.tickets WHERE Status = 'Open'")).Single();
        var closed = await host.UseAsync<ITicketService, TicketResponseDTO>(s =>
            s.RegisterExitByCodeAsync(openCode, new RegisterExitRequest()));
        closed.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task RemovingTheTicketCode_ShouldBeReversible()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = await ModuleHost.CreateMigratedAsync(databases);

        await MigrateAccessToAsync(host, BeforeTicketCode);

        (await UniqueIndexesAsync(databases.Access)).Should().NotContain("ux_tickets_code");
        (await ReadFirstColumnAsync(
            "SELECT COLUMN_NAME FROM information_schema.COLUMNS "
            + $"WHERE TABLE_SCHEMA = '{databases.Access}' AND TABLE_NAME = 'tickets'"))
            .Should().NotContain(["code", "exit_method", "exit_reason", "exit_operator_id"]);
    }

    [Fact]
    public async Task ExistingClients_ShouldBecomeIndividuals_WithTheRestrictedUser()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        var restricted = await RestrictedUserAsync(databases);
        await using var host = ModuleHost.Create(restricted);
        await MigrateAccessToAsync(host, BeforeClientType);
        await _mysql.ExecuteAsync(ClientInsert(databases.Access, "11144477735", out var id));

        await MigrateAccessToAsync(host, "AddClientType");

        (await ReadFirstColumnAsync($"SELECT type FROM {databases.Access}.clients WHERE Id = '{id}'"))
            .Should().ContainSingle().Which.Should().Be("Individual");
        (await ReadFirstColumnAsync($"SELECT document FROM {databases.Access}.clients WHERE Id = '{id}'"))
            .Should().ContainSingle().Which.Should().Be("11144477735");
        (await UniqueIndexesAsync(databases.Access)).Should().Contain("ux_clients_document").And.NotContain("ux_clients_cpf");
    }

    [Fact]
    public async Task RemovingTheClientType_ShouldRestoreTheCpfColumn()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateAccessToAsync(host, "AddClientType");

        await MigrateAccessToAsync(host, BeforeClientType);

        (await UniqueIndexesAsync(databases.Access)).Should().Contain("ux_clients_cpf").And.NotContain("ux_clients_document");
    }

    /// <summary>Usuário com acesso só aos schemas do teste, como o neoparking_app do compose.</summary>
    private async Task<TestDatabases> RestrictedUserAsync(TestDatabases databases)
    {
        var user = "app_" + Guid.NewGuid().ToString("N")[..12];
        await _mysql.ExecuteAsync(
            $"CREATE USER '{user}'@'%' IDENTIFIED BY '{MySqlFixture.RootPassword}'; "
            + $"GRANT ALL PRIVILEGES ON {databases.Access}.* TO '{user}'@'%'; "
            + $"GRANT ALL PRIVILEGES ON {databases.Management}.* TO '{user}'@'%'; "
            + $"GRANT ALL PRIVILEGES ON {databases.Billing}.* TO '{user}'@'%';");

        var forRoot = databases.ConnectionStringFor;
        return databases with { ConnectionStringFor = name => forRoot(name).Replace("Uid=root", $"Uid={user}") };
    }

    private static Task MigrateAccessToAsync(ModuleHost host, string migration) =>
        host.UseAsync<AccessDbContext>(c => c.GetService<IMigrator>().MigrateAsync(migration));

    private static async Task PendingShouldContainAsync<TContext>(ModuleHost host, Guid id) where TContext : DbContext
    {
        var pending = await host.UseAsync<OutboxRepository<TContext>, IReadOnlyList<OutboxMessage>>(r => r.GetPendingAsync());

        var message = pending.Should().ContainSingle(m => m.Id == id).Subject;
        message.AttemptCount.Should().Be(0);
        message.NextAttemptAt.Should().BeNull();
        message.DeadLetteredAt.Should().BeNull();
    }

    private static (string Seed, string Table, string Fix) AccessSeed(string rule, string database) => rule switch
    {
        "vehicles" => (
            ClientInsert(database, "11144477735", out var clientId)
            + VehicleInsert(database, "ABC1234", clientId) + VehicleInsert(database, "ABC1234", clientId),
            "vehicles",
            $"DELETE FROM {database}.vehicles;"),
        "clients" => (
            ClientInsert(database, "98765432100") + ClientInsert(database, "98765432100"),
            "clients",
            $"DELETE FROM {database}.clients;"),
        _ => (
            TicketInsert(database, "XYZ9876", "Open") + TicketInsert(database, "XYZ9876", "Open"),
            "tickets",
            $"UPDATE {database}.tickets SET Status = 'Closed', exit_at = {Now};"),
    };

    private static string ClientInsert(string database, string cpf) => ClientInsert(database, cpf, out _);

    private static string ClientInsert(string database, string cpf, out Guid id)
    {
        id = Guid.NewGuid();
        return $"INSERT INTO {database}.clients (Id, Name, cpf, phone_number) "
            + $"VALUES ('{id}', 'Cliente', '{cpf}', '+5511999999999');";
    }

    private static string VehicleInsert(string database, string plate, Guid clientId) =>
        $"INSERT INTO {database}.vehicles (Id, plate, ClientId) VALUES ('{Guid.NewGuid()}', '{plate}', '{clientId}');";

    private static string TicketInsert(string database, string plate, string status) =>
        $"INSERT INTO {database}.tickets (Id, plate, entry_at, Status) "
        + $"VALUES ('{Guid.NewGuid()}', '{plate}', {Now}, '{status}');";

    private static string OwnerInsert(string database, string name) =>
        $"INSERT INTO {database}.management_operators (Id, Name, Email, password_hash, Role, IsActive, CreatedAt) "
        + $"VALUES ('{Guid.NewGuid()}', '{name}', '{name}@neoparking.test', 'hash', 'Owner', 1, {Now});";

    private static string PolicyInsert(string database) =>
        $"INSERT INTO {database}.billing_pricing_policies "
        + "(Id, OwnerId, minimum_fee, hourly_rate, minimum_fee_coverage_minutes, grace_period_minutes, EffectiveFrom) "
        + $"VALUES ('{Guid.NewGuid()}', '{Guid.NewGuid()}', 10, 4, 60, 0, {Now});";

    private static string OutboxInsert(string table, Guid id) =>
        $"INSERT INTO {table} (Id, Type, Payload, CreatedAt) VALUES ('{id}', 'Some.Type', '{{}}', {Now});";

    private static string TableQuery(string database) =>
        $"SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = '{database}'";

    private async Task<long> CountAsync(string table)
    {
        await using var connection = await _mysql.OpenAsync();
        await using var command = new MySqlCommand($"SELECT COUNT(*) FROM {table}", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<List<string>> UniqueIndexesAsync(string database)
    {
        var sql = "SELECT DISTINCT INDEX_NAME FROM information_schema.STATISTICS "
            + $"WHERE TABLE_SCHEMA = '{database}' AND NON_UNIQUE = 0";
        return await ReadFirstColumnAsync(sql);
    }

    private async Task<List<string>> ReadFirstColumnAsync(string sql)
    {
        await using var connection = await _mysql.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        do
        {
            while (await reader.ReadAsync())
                values.Add(reader.GetString(0));
        }
        while (await reader.NextResultAsync());

        return values;
    }
}
