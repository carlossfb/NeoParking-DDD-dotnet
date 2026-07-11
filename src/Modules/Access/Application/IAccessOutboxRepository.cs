namespace NeoParking.Access.Application;

using NeoParking.Shared.Kernel.Outbox;

/// <summary>
/// Especialização de <see cref="IOutboxRepository"/> só para o Access. Existe unicamente
/// para dar um tipo de interface distinto no DI — como IOutboxRepository é registrada por
/// três módulos, resolver pela interface base sempre pega o último módulo registrado
/// (Billing), não o repositório correto do Access. Ver AccessModule.AddAccessModule.
/// </summary>
public interface IAccessOutboxRepository : IOutboxRepository
{
}
