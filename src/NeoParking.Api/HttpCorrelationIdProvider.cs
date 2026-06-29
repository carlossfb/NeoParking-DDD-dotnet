namespace NeoParking.Api;

using System.Diagnostics;
using NeoParking.Shared.Kernel.Observability;

/// <summary>
/// Lê o TraceId do Activity.Current — gerado automaticamente pelo ASP.NET para cada requisição.
/// Quando o sistema evoluir para distribuído, esse mesmo TraceId é propagado pelo
/// W3C Trace Context (header traceparent), sem nenhuma mudança de código.
/// </summary>
public sealed class HttpCorrelationIdProvider : ICorrelationIdProvider
{
    public Guid GetCorrelationId()
    {
        var traceId = Activity.Current?.TraceId.ToString();

        // TraceId do W3C tem 32 chars hex — converte para Guid
        if (!string.IsNullOrEmpty(traceId) && traceId.Length == 32)
        {
            Span<char> guidFormat = stackalloc char[36];
            traceId.AsSpan(0, 8).CopyTo(guidFormat);
            guidFormat[8] = '-';
            traceId.AsSpan(8, 4).CopyTo(guidFormat[9..]);
            guidFormat[13] = '-';
            traceId.AsSpan(12, 4).CopyTo(guidFormat[14..]);
            guidFormat[18] = '-';
            traceId.AsSpan(16, 4).CopyTo(guidFormat[19..]);
            guidFormat[23] = '-';
            traceId.AsSpan(20, 12).CopyTo(guidFormat[24..]);

            if (Guid.TryParse(guidFormat, out var guid))
                return guid;
        }

        // fallback: sem contexto HTTP (testes, background jobs)
        return Guid.NewGuid();
    }
}
