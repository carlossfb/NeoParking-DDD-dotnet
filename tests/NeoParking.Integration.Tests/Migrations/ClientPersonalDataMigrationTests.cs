namespace NeoParking.Integration.Tests.Migrations;

using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Access.Infrastructure;
using NeoParking.Integration.Tests.Support;

/// <summary>TD-710: nome e telefone passam por expandir, preencher, verificar e só então remover o texto puro.</summary>
[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ClientPersonalDataMigrationTests
{
    private const string BeforeDocumentProtection = "AddClientDocumentAccesses";
    private const string LegacyName = "Maria Legada de Souza";
    private const string LegacyPhone = "+5511912345678";
    private const string EventType = "NeoParking.Shared.Kernel.Events.ClientRegisteredIntegrationEvent";

    private readonly MySqlFixture _mysql;

    public ClientPersonalDataMigrationTests(MySqlFixture mysql) => _mysql = mysql;

    private static Task MigrateAsync(ModuleHost host) =>
        host.UseAsync<AccessDatabaseMigrator>(m => m.MigrateAsync());

    private static Task MigrateToAsync(ModuleHost host, string migration) =>
        host.UseAsync<AccessDbContext>(c => c.GetService<IMigrator>().MigrateAsync(migration));

    [Fact]
    public async Task FromScratch_ShouldEndWithoutPlainText_AndMatchTheModel()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);

        await MigrateAsync(host);

        (await ColumnsAsync(databases.Access)).Should().Contain(["name_encrypted", "phone_encrypted"])
            .And.NotContain(c => c.Equals("name", StringComparison.OrdinalIgnoreCase))
            .And.NotContain("phone_number");
        await host.UseAsync<AccessDbContext>(c =>
        {
            c.Database.HasPendingModelChanges().Should().BeFalse();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PreviousVersionData_ShouldBeEncrypted_AndStayReadable()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateToAsync(host, BeforeDocumentProtection);
        var id = Guid.NewGuid();
        await _mysql.ExecuteAsync(
            $"INSERT INTO {databases.Access}.clients (Id, Name, document, phone_number) "
            + $"VALUES ('{id}', '{LegacyName}', '11144477735', '{LegacyPhone}');");

        await MigrateAsync(host);

        var row = await AllClientColumnsAsync(databases.Access);
        row.Should().NotContain(LegacyName).And.NotContain("Legada").And.NotContain(LegacyPhone).And.NotContain("912345678");

        var view = await host.UseAsync<IClientService, ClientResponseDTO?>(s => s.GetClientByIdAsync(id));
        view!.Name.Should().Be(LegacyName);
        view.PhoneNumber.Should().Be(LegacyPhone);
    }

    [Fact]
    public async Task OutboxMessages_ShouldLoseTheName_AndStayDeliverable()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateToAsync(host, BeforeDocumentProtection);
        var clientId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        await _mysql.ExecuteAsync(
            $"INSERT INTO {databases.Access}.outbox_messages (Id, Type, Payload, CreatedAt, AttemptCount) VALUES ('{messageId}', '{EventType}', "
            + $"'{{\"ClientId\":\"{clientId}\",\"Name\":\"{LegacyName}\",\"CorrelationId\":\"{correlationId}\"}}', UTC_TIMESTAMP(), 0);");

        await MigrateAsync(host);

        var payload = await FirstColumnAsync($"SELECT Payload FROM {databases.Access}.outbox_messages WHERE Id = '{messageId}'");
        payload.Should().NotContain(LegacyName).And.NotContain("\"Name\"").And.Contain(clientId.ToString());

        var message = await host.ReloadFromAccessOutboxAsync(messageId);
        message.Deserialize().Should().BeOfType<NeoParking.Shared.Kernel.Events.ClientRegisteredIntegrationEvent>()
            .Which.ClientId.Should().Be(clientId);
    }

    [Fact]
    public async Task UnreadableRow_ShouldStopAndKeepThePlainText_ThenARetryShouldFinish()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateToAsync(host, AccessDatabaseMigrator.ExpandPersonalDataMigration);

        var unfilled = await InsertAsync(host, databases, "11144477735", "Pessoa Sem Cifra");
        var filled = await InsertAsync(host, databases, "98765432100", "Pessoa Ja Cifrada");
        var broken = await InsertAsync(host, databases, "52998224725", "Pessoa Quebrada");
        var filledValue = await ProtectAsync(host, ClientField.Name, "Pessoa Ja Cifrada");
        var filledPhone = await ProtectAsync(host, ClientField.PhoneNumber, LegacyPhone);
        await _mysql.ExecuteAsync(
            $"UPDATE {databases.Access}.clients SET name_encrypted = '{filledValue}', phone_encrypted = '{filledPhone}' WHERE Id = '{filled}';"
            + $"UPDATE {databases.Access}.clients SET name_encrypted = 'v1.AAAA', phone_encrypted = '{filledPhone}' WHERE Id = '{broken}';");

        var firstRun = () => MigrateAsync(host);

        var message = (await firstRun.Should().ThrowAsync<InvalidOperationException>()).Which.Message;
        message.Should().Contain(broken.ToString()).And.Contain("plain-text columns were kept")
            .And.NotContain("Pessoa").And.NotContain(LegacyPhone);
        (await ColumnsAsync(databases.Access)).Should().Contain("phone_number");
        (await ScalarAsync($"SELECT COUNT(*) FROM {databases.Access}.clients WHERE Id = '{unfilled}' AND name_encrypted IS NULL"))
            .Should().Be(1, "nada é preenchido enquanto houver uma linha que a chave não lê");

        await _mysql.ExecuteAsync($"UPDATE {databases.Access}.clients SET name_encrypted = NULL, phone_encrypted = NULL WHERE Id = '{broken}';");
        await MigrateAsync(host);

        (await ColumnsAsync(databases.Access)).Should().NotContain("phone_number");
        (await FirstColumnAsync($"SELECT name_encrypted FROM {databases.Access}.clients WHERE Id = '{filled}'"))
            .Should().Be(filledValue, "uma nova tentativa não refaz o que já foi preenchido");
        (await NameOfAsync(host, unfilled)).Should().Be("Pessoa Sem Cifra");
        (await NameOfAsync(host, filled)).Should().Be("Pessoa Ja Cifrada");
        (await NameOfAsync(host, broken)).Should().Be("Pessoa Quebrada");
    }

    [Fact]
    public async Task RetryWithAnotherKey_ShouldStop_WithoutMixingKeys()
    {
        var databases = await RestrictedAsync();
        await using var original = ModuleHost.Create(databases);
        await MigrateToAsync(original, AccessDatabaseMigrator.ExpandPersonalDataMigration);
        var filled = await InsertAsync(original, databases, "11144477735", "Pessoa Ja Cifrada");
        var unfilled = await InsertAsync(original, databases, "98765432100", "Pessoa Sem Cifra");
        var name = await ProtectAsync(original, ClientField.Name, "Pessoa Ja Cifrada");
        var phone = await ProtectAsync(original, ClientField.PhoneNumber, LegacyPhone);
        await _mysql.ExecuteAsync($"UPDATE {databases.Access}.clients SET name_encrypted = '{name}', phone_encrypted = '{phone}' WHERE Id = '{filled}';");

        await using var wrongKey = ModuleHost.Create(databases, RandomNumberGenerator.GetHexString(64));
        var act = () => MigrateAsync(wrongKey);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain(filled.ToString());
        (await ScalarAsync($"SELECT COUNT(*) FROM {databases.Access}.clients WHERE Id = '{unfilled}' AND name_encrypted IS NULL")).Should().Be(1);

        await MigrateAsync(original);

        (await NameOfAsync(original, filled)).Should().Be("Pessoa Ja Cifrada");
        (await NameOfAsync(original, unfilled)).Should().Be("Pessoa Sem Cifra");
    }

    [Fact]
    public async Task MigrateWithoutFilling_ShouldBeRefused_AndKeepThePlainText()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateToAsync(host, AccessDatabaseMigrator.ExpandPersonalDataMigration);
        await InsertAsync(host, databases, "11144477735", LegacyName);

        // É o caminho de dotnet ef database update, que não conhece as chaves.
        var act = () => host.UseAsync<AccessDbContext>(c => c.Database.MigrateAsync());

        (await act.Should().ThrowAsync<MySqlException>()).Which.Message.Should().Contain("not protected");
        (await ScalarAsync($"SELECT COUNT(*) FROM {databases.Access}.clients WHERE Name = '{LegacyName}'")).Should().Be(1);
    }

    [Fact]
    public async Task InterruptedRemoval_ShouldBeFinishedByARetry()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateToAsync(host, AccessDatabaseMigrator.ExpandPersonalDataMigration);
        var client = await InsertAsync(host, databases, "11144477735", LegacyName);
        var name = await ProtectAsync(host, ClientField.Name, LegacyName);
        var phone = await ProtectAsync(host, ClientField.PhoneNumber, LegacyPhone);

        // Como se a remoção tivesse parado depois do DROP, antes de registrar a migration.
        await _mysql.ExecuteAsync(
            $"UPDATE {databases.Access}.clients SET name_encrypted = '{name}', phone_encrypted = '{phone}' WHERE Id = '{client}';"
            + $"ALTER TABLE {databases.Access}.clients DROP COLUMN Name, DROP COLUMN phone_number;");

        await MigrateAsync(host);

        (await NameOfAsync(host, client)).Should().Be(LegacyName);
    }

    private static Task<string> ProtectAsync(ModuleHost host, ClientField field, string value) =>
        host.UseAsync<IDocumentProtector, string>(p => Task.FromResult(p.ProtectField(field, value)));

    private static Task<string?> NameOfAsync(ModuleHost host, Guid id) =>
        host.UseAsync<IClientService, string?>(async s => (await s.GetClientByIdAsync(id))?.Name);

    /// <summary>Uma linha da versão anterior: documento já protegido, nome e telefone ainda em texto puro.</summary>
    private async Task<Guid> InsertAsync(ModuleHost host, TestDatabases databases, string cpf, string name)
    {
        var id = Guid.NewGuid();
        var document = await host.UseAsync<IDocumentProtector, ProtectedDocument>(p =>
            Task.FromResult(p.Protect(Document.Create(ClientType.Individual, cpf))));
        await _mysql.ExecuteAsync(
            $"INSERT INTO {databases.Access}.clients (Id, Name, phone_number, type, document_encrypted, document_hmac) "
            + $"VALUES ('{id}', '{name}', '{LegacyPhone}', 'Individual', '{document.Encrypted}', '{document.Hmac}');");
        return id;
    }

    private async Task<TestDatabases> RestrictedAsync()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        var user = "app_" + Guid.NewGuid().ToString("N")[..12];
        await _mysql.ExecuteAsync(
            $"CREATE USER '{user}'@'%' IDENTIFIED BY '{MySqlFixture.RootPassword}'; "
            + $"GRANT ALL PRIVILEGES ON {databases.Access}.* TO '{user}'@'%'; "
            + $"GRANT ALL PRIVILEGES ON {databases.Management}.* TO '{user}'@'%'; "
            + $"GRANT ALL PRIVILEGES ON {databases.Billing}.* TO '{user}'@'%';");

        var forRoot = databases.ConnectionStringFor;
        return databases with { ConnectionStringFor = name => forRoot(name).Replace("Uid=root", $"Uid={user}") };
    }

    private async Task<List<string>> ColumnsAsync(string database)
    {
        await using var connection = await _mysql.OpenAsync();
        await using var command = new MySqlCommand(
            $"SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = '{database}' AND TABLE_NAME = 'clients'",
            connection);
        await using var reader = await command.ExecuteReaderAsync();

        var columns = new List<string>();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));

        return columns;
    }

    private async Task<string> AllClientColumnsAsync(string database)
    {
        await using var connection = await _mysql.OpenAsync();
        await using var command = new MySqlCommand($"SELECT * FROM {database}.clients", connection);
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        while (await reader.ReadAsync())
            values.AddRange(Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetValue(i).ToString()!));

        return string.Join('|', values);
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = await _mysql.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string?> FirstColumnAsync(string sql)
    {
        await using var connection = await _mysql.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync()) as string;
    }
}
