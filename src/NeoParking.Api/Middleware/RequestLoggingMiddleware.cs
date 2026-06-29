namespace NeoParking.Api.Middleware;

using System.Diagnostics;

/// <summary>
/// Loga cada requisição HTTP com TraceId, método, path, status e duração.
/// O TraceId é o mesmo que viaja nos eventos de domínio via ICorrelationIdProvider,
/// então é possível correlacionar uma requisição HTTP com todos os seus efeitos.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        await _next(context);

        sw.Stop();

        var traceId = Activity.Current?.TraceId.ToString() ?? "n/a";

        _logger.LogInformation(
            "{Method} {Path} → {StatusCode} in {ElapsedMs}ms | traceId: {TraceId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds,
            traceId);
    }
}
