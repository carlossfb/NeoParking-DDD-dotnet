namespace NeoParking.Shared.Kernel.Observability;

/// <summary>
/// Fornece o CorrelationId da operação atual.
/// Em contexto HTTP, retorna o TraceId da requisição (Activity.Current).
/// Isso garante que eventos de domínio ficam amarrados à requisição que os originou,
/// facilitando rastreamento distribuído quando o sistema evoluir.
/// </summary>
public interface ICorrelationIdProvider
{
    Guid GetCorrelationId();
}
