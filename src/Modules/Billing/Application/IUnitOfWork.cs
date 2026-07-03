namespace NeoParking.Billing.Application;

/// <summary>
/// Abstração de unidade de trabalho. Permite que a Application coordene o SaveChanges
/// (aggregate + outbox, atomicamente) sem depender diretamente do DbContext (Infrastructure).
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
