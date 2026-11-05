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
/// Aplica as migrations do Access com a proteção do documento em três fases: expandir, preencher
/// e só então remover o texto puro. O MySQL não tem AES-GCM e a chave não pode entrar no SQL,
/// por isso o preenchimento é feito aqui. Pode ser repetido sem efeito sobre o que já foi feito.
/// </summary>
public sealed class AccessDatabaseMigrator
{
    public const string ExpandMigration = "ExpandClientDocumentProtection";
    public const string ContractMigration = "ContractClientDocumentProtection";

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

        // Sem esta verificação, migrar para a fase de expansão desfaria a remoção já concluída.
        if (!applied.Any(id => id.EndsWith($"_{ContractMigration}", StringComparison.Ordinal)))
        {
            await _context.GetService<IMigrator>().MigrateAsync(ExpandMigration, cancellationToken);
            await ProtectExistingDocumentsAsync(cancellationToken);
        }

        await _context.Database.MigrateAsync(cancellationToken);
    }

    private async Task ProtectExistingDocumentsAsync(CancellationToken cancellationToken)
    {
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _context.Database.GetDbConnection();

            // Uma remoção interrompida pode já ter tirado a coluna; a migration termina o resto.
            if (!await PlainTextColumnExistsAsync(connection, cancellationToken))
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

    // Em lotes e ordenado por Id: a conexão fica livre para os UPDATEs entre um lote e outro.
    private static async IAsyncEnumerable<ClientRow> ReadAllAsync(
        DbConnection connection,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var lastId = string.Empty;

        while (true)
        {
            var batch = new List<ClientRow>(BatchSize);

            await using (var select = connection.CreateCommand())
            {
                select.CommandText =
                    "SELECT Id, type, document, document_encrypted, document_hmac FROM clients "
                    + $"WHERE Id > @lastId ORDER BY Id LIMIT {BatchSize}";
                AddParameter(select, "@lastId", lastId);

                await using var reader = await select.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    batch.Add(new ClientRow(
                        reader.GetValue(0).ToString()!,
                        reader.GetString(1),
                        reader.GetString(2),
                        await reader.IsDBNullAsync(3, cancellationToken) ? null : reader.GetString(3),
                        await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetString(4)));
                }
            }

            foreach (var row in batch)
                yield return row;

            if (batch.Count < BatchSize)
                yield break;

            lastId = batch[^1].Id;
        }
    }

    private static async Task<bool> PlainTextColumnExistsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.COLUMNS "
            + "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'clients' AND COLUMN_NAME = 'document'";
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
}
