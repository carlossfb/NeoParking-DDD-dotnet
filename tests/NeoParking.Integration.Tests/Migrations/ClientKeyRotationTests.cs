namespace NeoParking.Integration.Tests.Migrations;

using System.Security.Cryptography;
using FluentAssertions;
using MySqlConnector;
using NeoParking.Access.Application;
using NeoParking.Access.Infrastructure;
using NeoParking.Integration.Tests.Support;
using NeoParking.Shared.Kernel.Exceptions;

/// <summary>TD-711: a rotação recifra documento, nome e telefone no boot e pode ser retomada, com dados reais no MySQL.</summary>
[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ClientKeyRotationTests
{
    private const string FirstCpf = "11144477735";
    private const string SecondCpf = "98765432100";
    private const string ThirdCpf = "52998224725";
    private const string Phone = "+5511912345678";

    private static readonly string OriginalKey = RandomNumberGenerator.GetHexString(64);
    private static readonly string NewKey = RandomNumberGenerator.GetHexString(64);

    private readonly MySqlFixture _mysql;

    public ClientKeyRotationTests(MySqlFixture mysql) => _mysql = mysql;

    private static ModuleHost Original(TestDatabases databases) => ModuleHost.Create(databases, OriginalKey);

    private static ModuleHost Rotated(TestDatabases databases) =>
        ModuleHost.Create(databases, NewKey, keyVersion: 2, previousKeys: $"1:{OriginalKey}");

    private static ModuleHost OnlyNewKey(TestDatabases databases) =>
        ModuleHost.Create(databases, NewKey, keyVersion: 2);

    private static Task MigrateAsync(ModuleHost host) =>
        host.UseAsync<AccessDatabaseMigrator>(m => m.MigrateAsync());

    private static Task<ClientResponseDTO> CreateAsync(ModuleHost host, string cpf, string name) =>
        host.UseAsync<IClientService, ClientResponseDTO>(s => s.CreateClientAsync(
            new ClientRequestDTO(name, Phone, "Individual", cpf)));

    private static Task<string> DocumentOfAsync(ModuleHost host, Guid id) =>
        host.UseAsync<IClientService, string>(async s => (await s.RevealDocumentAsync(id, Guid.NewGuid())).Document);

    [Fact]
    public async Task Rotation_ShouldReencryptEveryField_AndKeepTheValuesAndTheUniqueness()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var original = Original(databases);
        await MigrateAsync(original);
        var first = await CreateAsync(original, FirstCpf, "Pessoa Um");
        var second = await CreateAsync(original, SecondCpf, "Pessoa Dois");
        var hmacs = await TextAsync($"SELECT GROUP_CONCAT(document_hmac ORDER BY Id) FROM {databases.Access}.clients");

        await using var rotated = Rotated(databases);
        await MigrateAsync(rotated);

        (await CountOutdatedAsync(databases, "v2.")).Should().Be(0);
        (await TextAsync($"SELECT GROUP_CONCAT(document_hmac ORDER BY Id) FROM {databases.Access}.clients")).Should().Be(hmacs);

        // Sem a chave antiga: prova que nada mais depende dela.
        await using var onlyNew = OnlyNewKey(databases);
        await MigrateAsync(onlyNew);
        (await DocumentOfAsync(onlyNew, first.Id)).Should().Be(FirstCpf);
        (await DocumentOfAsync(onlyNew, second.Id)).Should().Be(SecondCpf);
        var view = await onlyNew.UseAsync<IClientService, ClientResponseDTO?>(s => s.GetClientByIdAsync(first.Id));
        view!.Name.Should().Be("Pessoa Um");
        view.PhoneNumber.Should().Be(Phone);

        var duplicate = () => CreateAsync(onlyNew, FirstCpf, "Outra Pessoa");
        await duplicate.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Rotation_ShouldBeInterruptible_AndARetryShouldFinish()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var original = Original(databases);
        await MigrateAsync(original);
        var first = await CreateAsync(original, FirstCpf, "Pessoa Um");
        var broken = await CreateAsync(original, SecondCpf, "Pessoa Quebrada");
        var third = await CreateAsync(original, ThirdCpf, "Pessoa Tres");
        var goodValue = await TextAsync($"SELECT name_encrypted FROM {databases.Access}.clients WHERE Id = '{broken.Id}'");
        await _mysql.ExecuteAsync($"UPDATE {databases.Access}.clients SET name_encrypted = 'v1.AAAA' WHERE Id = '{broken.Id}';");

        await using var rotated = Rotated(databases);
        var interrupted = () => MigrateAsync(rotated);

        var message = (await interrupted.Should().ThrowAsync<InvalidOperationException>()).Which.Message;
        message.Should().Contain(broken.Id.ToString()).And.NotContain("Pessoa").And.NotContain(SecondCpf).And.NotContain(Phone);
        (await CountOutdatedAsync(databases, "v2.")).Should().Be(1, "as outras linhas terminam, e só a ilegível fica para trás");
        (await TextAsync($"SELECT document_encrypted FROM {databases.Access}.clients WHERE Id = '{broken.Id}'"))
            .Should().StartWith("v1.", "a linha muda inteira ou não muda");

        var rotatedValue = await TextAsync($"SELECT name_encrypted FROM {databases.Access}.clients WHERE Id = '{first.Id}'");
        await _mysql.ExecuteAsync($"UPDATE {databases.Access}.clients SET name_encrypted = '{goodValue}' WHERE Id = '{broken.Id}';");

        await MigrateAsync(rotated);

        (await CountOutdatedAsync(databases, "v2.")).Should().Be(0);
        (await TextAsync($"SELECT name_encrypted FROM {databases.Access}.clients WHERE Id = '{first.Id}'"))
            .Should().Be(rotatedValue, "uma nova tentativa não refaz o que já foi recifrado");
        (await DocumentOfAsync(rotated, broken.Id)).Should().Be(SecondCpf);
        (await DocumentOfAsync(rotated, third.Id)).Should().Be(ThirdCpf);
    }

    [Fact]
    public async Task MissingOldKey_ShouldStopTheBoot_WithoutChangingAnyRow()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var original = Original(databases);
        await MigrateAsync(original);
        var client = await CreateAsync(original, FirstCpf, "Pessoa Um");
        var before = await AllRowsAsync(databases);

        await using var withoutOldKey = OnlyNewKey(databases);
        var act = () => MigrateAsync(withoutOldKey);

        var message = (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message;
        message.Should().Contain(client.Id.ToString()).And.Contain(DocumentProtectionKeys.PreviousKeysName)
            .And.NotContain(OriginalKey).And.NotContain(NewKey).And.NotContain(FirstCpf);
        (await AllRowsAsync(databases)).Should().Be(before);

        await using var rotated = Rotated(databases);
        await MigrateAsync(rotated);
        (await DocumentOfAsync(rotated, client.Id)).Should().Be(FirstCpf);
    }

    [Fact]
    public async Task BeforeRotating_NewWrites_ShouldUseTheCurrentKey_AndOldRowsStayReadable()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var original = Original(databases);
        await MigrateAsync(original);
        var old = await CreateAsync(original, FirstCpf, "Pessoa Antiga");

        await using var rotated = Rotated(databases);
        var created = await CreateAsync(rotated, SecondCpf, "Pessoa Nova");

        (await TextAsync($"SELECT name_encrypted FROM {databases.Access}.clients WHERE Id = '{created.Id}'")).Should().StartWith("v2.");
        (await TextAsync($"SELECT name_encrypted FROM {databases.Access}.clients WHERE Id = '{old.Id}'")).Should().StartWith("v1.");
        (await DocumentOfAsync(rotated, old.Id)).Should().Be(FirstCpf);
    }

    [Fact]
    public async Task WithoutRotation_ABoot_ShouldNotRewriteAnything()
    {
        var databases = await _mysql.CreateDatabasesAsync();
        await using var original = Original(databases);
        await MigrateAsync(original);
        await CreateAsync(original, FirstCpf, "Pessoa Um");
        var before = await AllRowsAsync(databases);

        await MigrateAsync(original);

        (await AllRowsAsync(databases)).Should().Be(before);
    }

    private async Task<long> CountOutdatedAsync(TestDatabases databases, string prefix)
    {
        await using var connection = await _mysql.OpenAsync();
        await using var command = new MySqlCommand(
            $"SELECT COUNT(*) FROM {databases.Access}.clients WHERE document_encrypted NOT LIKE '{prefix}%' "
            + $"OR name_encrypted NOT LIKE '{prefix}%' OR phone_encrypted NOT LIKE '{prefix}%'",
            connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> TextAsync(string sql)
    {
        await using var connection = await _mysql.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private Task<string> AllRowsAsync(TestDatabases databases) => TextAsync(
        "SELECT GROUP_CONCAT(CONCAT_WS('|', document_encrypted, document_hmac, name_encrypted, phone_encrypted) ORDER BY Id) "
        + $"FROM {databases.Access}.clients");
}
