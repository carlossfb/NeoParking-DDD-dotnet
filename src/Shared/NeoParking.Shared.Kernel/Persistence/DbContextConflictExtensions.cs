namespace NeoParking.Shared.Kernel.Persistence;

using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NeoParking.Shared.Kernel.Exceptions;

public static class DbContextConflictExtensions
{
    /// <summary>
    /// Salva e traduz violação de índice único (erro MySQL 1062) para <see cref="ConflictException"/>.
    /// O índice é a garantia de unicidade; a mensagem genérica não expõe valores nem nomes do banco.
    /// </summary>
    public static async Task SaveChangesOrConflictAsync(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            throw new ConflictException("The operation conflicts with existing data.", ex);
        }
    }
}
