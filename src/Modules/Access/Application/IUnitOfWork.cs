namespace NeoParking.Access.Application;

/// <summary>
/// Abstração de unidade de trabalho. Permite que a Application coordene
/// o SaveChanges sem depender diretamente do DbContext (Infrastructure).
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
