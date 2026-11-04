namespace NeoParking.Integration.Tests.Migrations;

using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;
using NeoParking.Access.Application;
using NeoParking.Access.Infrastructure;
using NeoParking.Integration.Tests.Support;
using NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// TD-605: expandir, preencher e só então remover o texto puro. Sempre com um usuário restrito,
/// como o neoparking_app do compose. As linhas semeadas com CPF vêm da versão anterior.
/// </summary>
[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DocumentProtectionMigrationTests
{
    private const string BeforeProtection = "AddClientDocumentAccesses";
    private const string PreviousVersion = "AddTicketCode";
    private const string FirstCpf = "11144477735";
    private const string SecondCpf = "98765432100";
    private const string ValidCpfForFix = "52998224725";
    private const string InvalidCpf = "00000000000";

    private readonly MySqlFixture _mysql;

    public DocumentProtectionMigrationTests(MySqlFixture mysql) => _mysql = mysql;

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

        (await ColumnsAsync(databases.Access)).Should().Contain(["document_encrypted", "document_hmac", "type"])
            .And.NotContain(["document", "cpf"]);
        (await ScalarAsync(UniqueIndexQuery(databases.Access, "ux_clients_document_hmac"))).Should().Be(1);
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
        await MigrateToAsync(host, PreviousVersion);
        await _mysql.ExecuteAsync(
            CpfInsert(databases.Access, FirstCpf, out var first)
            + CpfInsert(databases.Access, SecondCpf, out var second));

        await MigrateAsync(host);

        (await ColumnsAsync(databases.Access)).Should().NotContain(["document", "cpf"]);
        var rows = await AllClientColumnsAsync(databases.Access);
        rows.Should().NotContain(FirstCpf).And.NotContain(SecondCpf).And.NotContain(FirstCpf[3..9]);

        (await RevealAsync(host, first)).Should().Be(FirstCpf);
        (await RevealAsync(host, second)).Should().Be(SecondCpf);
        var view = await host.UseAsync<IClientService, ClientResponseDTO?>(s => s.GetClientByIdAsync(first));
        view!.Type.Should().Be("Individual");
        view.Document.Should().Be("***.444.777-**");
    }

    [Fact]
    public async Task PreviousVersionData_ShouldStillBeUnique()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateToAsync(host, PreviousVersion);
        await _mysql.ExecuteAsync(CpfInsert(databases.Access, FirstCpf, out _));
        await MigrateAsync(host);

        var act = () => host.UseAsync<IClientService, ClientResponseDTO>(s => s.CreateClientAsync(
            new ClientRequestDTO("Cliente", "+5511999999999", "Individual", FirstCpf)));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task InvalidRow_ShouldStopBeforeRemovingThePlainText_ThenARetryShouldFinish()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateToAsync(host, BeforeProtection);
        await _mysql.ExecuteAsync(
            DocumentInsert(databases.Access, FirstCpf, out var valid)
            + DocumentInsert(databases.Access, InvalidCpf, out var invalid));

        var firstRun = () => MigrateAsync(host);

        var message = (await firstRun.Should().ThrowAsync<InvalidOperationException>()).Which.Message;
        message.Should().Contain(invalid.ToString()).And.Contain("plain-text column was kept")
            .And.NotContain(InvalidCpf).And.NotContain(FirstCpf);
        (await ColumnsAsync(databases.Access)).Should().Contain("document");
        (await ScalarAsync($"SELECT COUNT(*) FROM {databases.Access}.clients WHERE document = '{InvalidCpf}'")).Should().Be(1);
        (await ScalarAsync($"SELECT COUNT(*) FROM {databases.Access}.clients WHERE document = '{FirstCpf}'")).Should().Be(1);
        var filledBefore = await FirstColumnAsync(
            $"SELECT document_encrypted FROM {databases.Access}.clients WHERE Id = '{valid}'");
        filledBefore.Should().NotBeNullOrEmpty();

        await _mysql.ExecuteAsync($"UPDATE {databases.Access}.clients SET document = '{ValidCpfForFix}' WHERE Id = '{invalid}';");
        await MigrateAsync(host);

        (await ColumnsAsync(databases.Access)).Should().NotContain("document");
        (await RevealAsync(host, valid)).Should().Be(FirstCpf);
        (await RevealAsync(host, invalid)).Should().Be(ValidCpfForFix);
        (await FirstColumnAsync($"SELECT document_encrypted FROM {databases.Access}.clients WHERE Id = '{valid}'"))
            .Should().Be(filledBefore, "uma nova tentativa não refaz o que já foi preenchido");
    }

    [Fact]
    public async Task RetryWithAnotherKey_ShouldStop_AndTheOriginalKeyShouldFinish()
    {
        var databases = await RestrictedAsync();
        await using var original = ModuleHost.Create(databases);
        await MigrateToAsync(original, BeforeProtection);
        await _mysql.ExecuteAsync(
            DocumentInsert(databases.Access, FirstCpf, out var client)
            + DocumentInsert(databases.Access, InvalidCpf, out var invalid));
        var interrupted = () => MigrateAsync(original);
        await interrupted.Should().ThrowAsync<InvalidOperationException>();
        await _mysql.ExecuteAsync($"UPDATE {databases.Access}.clients SET document = '{SecondCpf}' WHERE Id = '{invalid}';");

        await using var wrongKey = ModuleHost.Create(databases, RandomNumberGenerator.GetHexString(64));
        var withWrongKey = () => MigrateAsync(wrongKey);

        (await withWrongKey.Should().ThrowAsync<InvalidOperationException>()).Which.Message
            .Should().Contain(client.ToString()).And.NotContain(FirstCpf);
        (await ColumnsAsync(databases.Access)).Should().Contain("document");

        (await ScalarAsync($"SELECT COUNT(*) FROM {databases.Access}.clients WHERE document_encrypted IS NULL")).Should()
            .Be(1, "a chave errada é recusada antes de preencher, e as duas chaves não se misturam");

        await MigrateAsync(original);

        (await RevealAsync(original, client)).Should().Be(FirstCpf);
        (await RevealAsync(original, invalid)).Should().Be(SecondCpf);
    }

    [Fact]
    public async Task MigrateWithoutFilling_ShouldBeRefused_AndKeepThePlainText()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateToAsync(host, BeforeProtection);
        await _mysql.ExecuteAsync(DocumentInsert(databases.Access, FirstCpf, out _));

        // É o caminho de dotnet ef database update, que não conhece as chaves.
        var act = () => host.UseAsync<AccessDbContext>(c => c.Database.MigrateAsync());

        (await act.Should().ThrowAsync<MySqlException>()).Which.Message.Should().Contain("not protected");
        (await ScalarAsync($"SELECT COUNT(*) FROM {databases.Access}.clients WHERE document = '{FirstCpf}'")).Should().Be(1);

        await MigrateAsync(host);
        (await ColumnsAsync(databases.Access)).Should().NotContain("document");
    }

    [Fact]
    public async Task InterruptedRemoval_ShouldBeFinishedByARetry()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateToAsync(host, BeforeProtection);
        await _mysql.ExecuteAsync(DocumentInsert(databases.Access, FirstCpf, out var client));
        var migrate = () => MigrateAsync(host);
        await MigrateToAsync(host, AccessDatabaseMigrator.ExpandMigration);
        await _mysql.ExecuteAsync(DocumentInsert(databases.Access, InvalidCpf, out var invalid));
        await migrate.Should().ThrowAsync<InvalidOperationException>();
        await _mysql.ExecuteAsync($"DELETE FROM {databases.Access}.clients WHERE Id = '{invalid}';");

        // Como se a remoção tivesse parado depois do índice e do DROP, antes de registrar a migration.
        await _mysql.ExecuteAsync(
            $"CREATE UNIQUE INDEX ux_clients_document_hmac ON {databases.Access}.clients (document_hmac); "
            + $"ALTER TABLE {databases.Access}.clients DROP COLUMN document;");

        await MigrateAsync(host);

        (await ColumnsAsync(databases.Access)).Should().NotContain("document");
        (await RevealAsync(host, client)).Should().Be(FirstCpf);
    }

    [Fact]
    public async Task MigrateAgain_AfterFinishing_ShouldNotUndoAnything()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateAsync(host);
        var created = await host.UseAsync<IClientService, ClientResponseDTO>(s => s.CreateClientAsync(
            new ClientRequestDTO("Cliente", "+5511999999999", "Individual", FirstCpf)));

        await MigrateAsync(host);

        (await ColumnsAsync(databases.Access)).Should().NotContain("document");
        (await RevealAsync(host, created.Id)).Should().Be(FirstCpf);
    }

    [Fact]
    public async Task Reverting_WithClients_ShouldBeRefused_AndKeepTheEncryptedDocuments()
    {
        var databases = await RestrictedAsync();
        await using var host = ModuleHost.Create(databases);
        await MigrateAsync(host);
        var created = await host.UseAsync<IClientService, ClientResponseDTO>(s => s.CreateClientAsync(
            new ClientRequestDTO("Cliente", "+5511999999999", "Individual", FirstCpf)));

        var act = () => MigrateToAsync(host, AccessDatabaseMigrator.ExpandMigration);

        (await act.Should().ThrowAsync<MySqlException>()).Which.Message.Should().Contain("Restore a backup");
        (await RevealAsync(host, created.Id)).Should().Be(FirstCpf);
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

    private static Task<string> RevealAsync(ModuleHost host, Guid id) =>
        host.UseAsync<IClientService, string>(async s =>
            (await s.RevealDocumentAsync(id, Guid.NewGuid())).Document);

    private static string CpfInsert(string database, string cpf, out Guid id)
    {
        id = Guid.NewGuid();
        return $"INSERT INTO {database}.clients (Id, Name, cpf, phone_number) "
            + $"VALUES ('{id}', 'Cliente', '{cpf}', '+5511999999999');";
    }

    private static string DocumentInsert(string database, string document, out Guid id)
    {
        id = Guid.NewGuid();
        return $"INSERT INTO {database}.clients (Id, Name, document, phone_number) "
            + $"VALUES ('{id}', 'Cliente', '{document}', '+5511999999999');";
    }

    private static string UniqueIndexQuery(string database, string index) =>
        "SELECT COUNT(*) FROM information_schema.STATISTICS "
        + $"WHERE TABLE_SCHEMA = '{database}' AND INDEX_NAME = '{index}' AND NON_UNIQUE = 0";

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
