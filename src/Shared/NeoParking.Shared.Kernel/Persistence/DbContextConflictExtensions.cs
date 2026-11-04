namespace NeoParking.Shared.Kernel.Persistence;

using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using NeoParking.Shared.Kernel.Exceptions;

public static class DbContextConflictExtensions
{
    /// <summary>
    /// Salva e traduz violação de índice único (erro MySQL 1062) para <see cref="ConflictException"/>.
    /// O índice é a garantia de unicidade. A exceção do banco não é mantida como interna, porque o texto
    /// do erro 1062 traz o valor duplicado e toda exceção não tratada vai para o log.
    /// Uma escrita perdida para outra operação, detectada por token de concorrência, é traduzida da mesma forma.
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
            throw new ConflictException("The operation conflicts with existing data.");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("The data was changed by another operation.", ex);
        }
    }
}
