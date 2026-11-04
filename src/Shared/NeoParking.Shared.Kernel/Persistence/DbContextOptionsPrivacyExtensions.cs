namespace NeoParking.Shared.Kernel.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public static class DbContextOptionsPrivacyExtensions
{
    /// <summary>
    /// O EF registra falhas de banco com a mensagem do servidor, e o erro MySQL 1062 traz o valor
    /// duplicado. A exceção continua subindo e é tratada por quem chamou.
    /// </summary>
    public static DbContextOptionsBuilder WithoutDatabaseErrorValuesInLogs(this DbContextOptionsBuilder options) =>
        options.ConfigureWarnings(warnings => warnings.Ignore(
            CoreEventId.SaveChangesFailed,
            RelationalEventId.CommandError));
}
