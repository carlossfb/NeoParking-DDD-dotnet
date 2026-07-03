namespace NeoParking.Billing.Application;

using NeoParking.Shared.Kernel.Outbox;

/// <summary>
/// Especialização de <see cref="IOutboxRepository"/> só para o Billing. Existe unicamente
/// para dar um tipo de interface distinto no DI — ver IAccessOutboxRepository para o
/// racional completo (mesma colisão, mesma solução).
/// </summary>
public interface IBillingOutboxRepository : IOutboxRepository
{
}
