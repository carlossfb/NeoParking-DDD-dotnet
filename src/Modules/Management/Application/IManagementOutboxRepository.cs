namespace NeoParking.Management.Application;

using NeoParking.Shared.Kernel.Outbox;

/// <summary>
/// Especialização de <see cref="IOutboxRepository"/> só para o Management. Existe unicamente
/// para dar um tipo de interface distinto no DI — ver IAccessOutboxRepository para o
/// racional completo (mesma colisão, mesma solução). Nenhum serviço do Management publica
/// eventos hoje, mas o registro segue o mesmo padrão para não reintroduzir a colisão
/// no dia em que alguém precisar.
/// </summary>
public interface IManagementOutboxRepository : IOutboxRepository
{
}
