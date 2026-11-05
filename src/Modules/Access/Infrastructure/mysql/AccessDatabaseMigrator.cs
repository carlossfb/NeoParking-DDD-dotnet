namespace NeoParking.Access.Infrastructure;

using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// Aplica as migrations do Access com a proteção do documento, e depois a do nome e do telefone, em três
/// fases: expandir, preencher e só então remover o texto puro. O MySQL não tem AES-GCM e a chave não pode
/// entrar no SQL, por isso o preenchimento é feito aqui. Pode ser repetido sem efeito sobre o que já foi feito.
/// </summary>
public sealed class AccessDatabaseMigrator
{
    public const string ExpandMigration = "ExpandClientDocumentProtection";
    public const string ContractMigration = "ContractClientDocumentProtection";
    public const string ExpandPersonalDataMigration = "ExpandClientPersonalDataProtection";
    public const string ContractPersonalDataMigration = "ContractClientPersonalDataProtection";

    private const int BatchSize = 500;
    private const int MaxIdsInMessage = 20;

    private readonly AccessDbContext _context;
    private readonly IDocumentProtector _protector;
    private readonly ILogger<AccessDatabaseMigrator> _logger;

    public AccessDatabaseMigrator(
        AccessDbContext context,
        IDocumentProtector protector,
        ILogger<AccessDatabaseMigrator> logger)
    {
        _context = context;
        _protector = protector;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var applied = await _context.Database.GetAppliedMigrationsAsync(cancellationToken);

        var migrator = _context.GetService<IMigrator>();

        // Sem estas verificações, migrar para a fase de expansão desfaria a remoção já concluída.
        if (!applied.Any(id => id.EndsWith($"_{ContractMigration}", StringComparison.Ordinal)))
        {
            await migrator.MigrateAsync(ExpandMigration, cancellationToken);
            await ProtectExistingDocumentsAsync(cancellationToken);
            await migrator.MigrateAsync(ContractMigration, cancellationToken);
        }

        if (!applied.Any(id => id.EndsWith($"_{ContractPersonalDataMigration}", StringComparison.Ordinal)))
        {
            await migrator.MigrateAsync(ExpandPersonalDataMigration, cancellationToken);
            await ProtectExistingPersonalDataAsync(cancellationToken);
        }

        await _context.Database.MigrateAsync(cancellationToken);
        await RotateKeysAsync(cancellationToken);
    }

    /// <summary>
    /// Recifra com a chave atual as linhas gravadas com uma anterior. Cada linha muda inteira ou não muda, e só se
    /// continuar igual ao que foi lido. As linhas ilegíveis ficam para o fim: as outras não precisam esperar, e uma
    /// nova tentativa só pega o que sobrou. Sem nada para recifrar, é só uma consulta.
    /// </summary>
    private async Task RotateKeysAsync(CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _context.Database.GetDbConnection();
            var current = $"{_protector.CurrentKeyVersion}.%";
            var failed = new List<string>();
            var rotated = 0;

            await foreach (var row in ReadBatchesAsync(
                connection,
                "type, document_encrypted, document_hmac, name_encrypted, phone_encrypted",
                (id, reader) => new RotationRow(
                    id, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)),
                cancellationToken,
                $"(document_encrypted NOT LIKE '{current}' OR name_encrypted NOT LIKE '{current}' OR phone_encrypted NOT LIKE '{current}')"))
            {
                if (!TryRotate(row, out var document, out var name, out var phone))
                {
                    failed.Add(row.Id);
                    continue;
                }

                await using var update = connection.CreateCommand();
                update.CommandText =
                    "UPDATE clients SET document_encrypted = @document, name_encrypted = @name, phone_encrypted = @phone "
                    + "WHERE Id = @id AND document_encrypted = @oldDocument AND name_encrypted = @oldName AND phone_encrypted = @oldPhone";
                AddParameter(update, "@document", document);
                AddParameter(update, "@name", name);
                AddParameter(update, "@phone", phone);
                AddParameter(update, "@id", row.Id);
                AddParameter(update, "@oldDocument", row.Document);
                AddParameter(update, "@oldName", row.Name);
                AddParameter(update, "@oldPhone", row.Phone);
                rotated += await update.ExecuteNonQueryAsync(cancellationToken);
            }

            if (rotated > 0)
                _logger.LogInformation("Key rotation: {Rotated} clients encrypted again with {Version}.", rotated, _protector.CurrentKeyVersion);

            if (failed.Count > 0)
                throw new InvalidOperationException(
                    $"Cannot finish rotating the client data key: {failed.Count} client(s) could not be read. Those rows were "
                    + $"not changed, and the others are done. Add the old key to {DocumentProtectionKeys.PreviousKeysName} or "
                    + $"fix these rows, then start again. First ids: {string.Join(", ", failed.Take(MaxIdsInMessage))}");
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private bool TryRotate(RotationRow row, out string document, out string name, out string phone)
    {
        try
        {
            document = _protector.RotateDocument(new ProtectedDocument(ClientTypes.Parse(row.Type), row.Document, row.Hmac)).Encrypted;
            name = _protector.ProtectField(ClientField.Name, _protector.RevealField(ClientField.Name, row.Name));
            phone = _protector.ProtectField(ClientField.PhoneNumber, _protector.RevealField(ClientField.PhoneNumber, row.Phone));
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or DomainException)
        {
            document = name = phone = string.Empty;
            return false;
        }
    }

    private async Task ProtectExistingPersonalDataAsync(CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _context.Database.GetDbConnection();

            // Uma remoção interrompida pode já ter tirado as colunas; a migration termina o resto.
            if (!await ColumnExistsAsync(connection, "phone_number", cancellationToken))
                return;

            // Antes de preencher: com outra chave, uma nova tentativa misturaria duas chaves no banco.
            ThrowIfAnyPersonalDataFailed(await VerifyPersonalDataAsync(connection, includeUnfilled: false, cancellationToken));

            var filled = await FillPersonalDataAsync(connection, cancellationToken);

            ThrowIfAnyPersonalDataFailed(await VerifyPersonalDataAsync(connection, includeUnfilled: true, cancellationToken));

            _logger.LogInformation("Client names and phones protected: {Filled} newly encrypted, all verified.", filled);
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private static void ThrowIfAnyPersonalDataFailed(List<string> failed)
    {
        if (failed.Count > 0)
            throw new InvalidOperationException(
                $"Cannot finish protecting client names and phones: {failed.Count} client(s) have a value that the "
                + "configured keys cannot read. The plain-text columns were kept. Fix these rows or restore the "
                + $"original keys, then start again. First ids: {string.Join(", ", failed.Take(MaxIdsInMessage))}");
    }

    private async Task<int> FillPersonalDataAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var filled = 0;

        await foreach (var row in ReadPersonalDataAsync(connection, cancellationToken))
        {
            if (row.NameEncrypted is not null && row.PhoneEncrypted is not null)
                continue;

            await using var update = connection.CreateCommand();
            update.CommandText = "UPDATE clients SET name_encrypted = @name, phone_encrypted = @phone WHERE Id = @id";
            AddParameter(update, "@name", _protector.ProtectField(ClientField.Name, row.Name));
            AddParameter(update, "@phone", _protector.ProtectField(ClientField.PhoneNumber, row.Phone));
            AddParameter(update, "@id", row.Id);
            await update.ExecuteNonQueryAsync(cancellationToken);

            filled++;
        }

        return filled;
    }

    /// <summary>Confere cada linha contra o texto puro, inclusive as de uma execução anterior.</summary>
    private async Task<List<string>> VerifyPersonalDataAsync(
        DbConnection connection, bool includeUnfilled, CancellationToken cancellationToken)
    {
        var failed = new List<string>();

        await foreach (var row in ReadPersonalDataAsync(connection, cancellationToken))
        {
            var isUnfilled = row.NameEncrypted is null && row.PhoneEncrypted is null;

            if ((includeUnfilled || !isUnfilled) && !IsVerified(row))
                failed.Add(row.Id);
        }

        return failed;
    }

    private bool IsVerified(PersonalDataRow row)
    {
        if (row.NameEncrypted is null || row.PhoneEncrypted is null)
            return false;

        try
        {
            return _protector.RevealField(ClientField.Name, row.NameEncrypted) == row.Name
                && _protector.RevealField(ClientField.PhoneNumber, row.PhoneEncrypted) == row.Phone;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static IAsyncEnumerable<PersonalDataRow> ReadPersonalDataAsync(DbConnection connection, CancellationToken cancellationToken) =>
        ReadBatchesAsync(
            connection,
            "Name, phone_number, name_encrypted, phone_encrypted",
            (id, reader) => new PersonalDataRow(
                id,
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)),
            cancellationToken);

    private async Task ProtectExistingDocumentsAsync(CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _context.Database.GetDbConnection();

            // Uma remoção interrompida pode já ter tirado a coluna; a migration termina o resto.
            if (!await ColumnExistsAsync(connection, "document", cancellationToken))
                return;

            // Antes de preencher: com outra chave, uma nova tentativa misturaria duas chaves no banco.
            ThrowIfAnyFailed(await VerifyAsync(connection, includeUnfilled: false, cancellationToken));

            var filled = await FillAsync(connection, cancellationToken);

            ThrowIfAnyFailed(await VerifyAsync(connection, includeUnfilled: true, cancellationToken));

            _logger.LogInformation("Client documents protected: {Filled} newly encrypted, all verified.", filled);
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private static void ThrowIfAnyFailed(List<string> failed)
    {
        if (failed.Count > 0)
            throw new InvalidOperationException(
                $"Cannot finish protecting client documents: {failed.Count} client(s) have an invalid document or one "
                + "that the configured keys cannot read. The plain-text column was kept. Fix these rows or restore the "
                + $"original keys, then start again. First ids: {string.Join(", ", failed.Take(MaxIdsInMessage))}");
    }

    private async Task<int> FillAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var filled = 0;

        await foreach (var row in ReadAllAsync(connection, cancellationToken))
        {
            if (row.Encrypted is not null && row.Hmac is not null)
                continue;

            if (!TryCreateDocument(row, out var document))
                continue;

            var protectedDocument = _protector.Protect(document);

            await using var update = connection.CreateCommand();
            update.CommandText =
                "UPDATE clients SET document_encrypted = @encrypted, document_hmac = @hmac WHERE Id = @id";
            AddParameter(update, "@encrypted", protectedDocument.Encrypted);
            AddParameter(update, "@hmac", protectedDocument.Hmac);
            AddParameter(update, "@id", row.Id);
            await update.ExecuteNonQueryAsync(cancellationToken);

            filled++;
        }

        return filled;
    }

    /// <summary>Confere cada linha contra o texto puro, inclusive as de uma execução anterior.</summary>
    private async Task<List<string>> VerifyAsync(
        DbConnection connection, bool includeUnfilled, CancellationToken cancellationToken)
    {
        var failed = new List<string>();

        await foreach (var row in ReadAllAsync(connection, cancellationToken))
        {
            var isUnfilled = row.Encrypted is null && row.Hmac is null;

            if ((includeUnfilled || !isUnfilled) && !IsVerified(row))
                failed.Add(row.Id);
        }

        return failed;
    }

    private bool IsVerified(ClientRow row)
    {
        if (row.Encrypted is null || row.Hmac is null || !TryCreateDocument(row, out var document))
            return false;

        try
        {
            return _protector.Reveal(new ProtectedDocument(document.Type, row.Encrypted, row.Hmac)) == document
                && _protector.Protect(document).Hmac == row.Hmac;
        }
        catch (Exception ex) when (ex is InvalidOperationException or DomainException)
        {
            return false;
        }
    }

    private static bool TryCreateDocument(ClientRow row, out Document document)
    {
        try
        {
            document = Document.Create(ClientTypes.Parse(row.Type), row.Plain);
            return true;
        }
        catch (DomainException)
        {
            document = null!;
            return false;
        }
    }

    private static IAsyncEnumerable<ClientRow> ReadAllAsync(DbConnection connection, CancellationToken cancellationToken) =>
        ReadBatchesAsync(
            connection,
            "type, document, document_encrypted, document_hmac",
            (id, reader) => new ClientRow(
                id,
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)),
            cancellationToken);

    // Em lotes e ordenado por Id: a conexão fica livre para os UPDATEs entre um lote e outro.
    private static async IAsyncEnumerable<T> ReadBatchesAsync<T>(
        DbConnection connection,
        string columns,
        Func<string, DbDataReader, T> map,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken,
        string? filter = null)
    {
        var lastId = string.Empty;

        while (true)
        {
            var batch = new List<T>(BatchSize);
            var batchLastId = lastId;

            await using (var select = connection.CreateCommand())
            {
                select.CommandText =
                    $"SELECT Id, {columns} FROM clients WHERE Id > @lastId {(filter is null ? "" : $"AND {filter} ")}ORDER BY Id LIMIT {BatchSize}";
                AddParameter(select, "@lastId", lastId);

                await using var reader = await select.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    batchLastId = reader.GetValue(0).ToString()!;
                    batch.Add(map(batchLastId, reader));
                }
            }

            foreach (var row in batch)
                yield return row;

            if (batch.Count < BatchSize)
                yield break;

            lastId = batchLastId;
        }
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string column, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.COLUMNS "
            + "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'clients' AND COLUMN_NAME = @column";
        AddParameter(command, "@column", column);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.String;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record ClientRow(string Id, string Type, string Plain, string? Encrypted, string? Hmac);

    private sealed record RotationRow(string Id, string Type, string Document, string Hmac, string Name, string Phone);

    private sealed record PersonalDataRow(string Id, string Name, string Phone, string? NameEncrypted, string? PhoneEncrypted);
}
