namespace NeoParking.Api.Extensions;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// O que logs e tracing registram no lugar do path: o padrão da rota, porque o path real
/// pode conter placa e código de ticket.
/// </summary>
public static class RouteLabel
{
    public const string Unmatched = "(no route)";

    public static string For(HttpContext context)
    {
        // Depois de uma exceção, o handler limpa o endpoint do contexto e o guarda nesta feature.
        var endpoint = context.GetEndpoint()
            ?? context.Features.Get<IExceptionHandlerPathFeature>()?.Endpoint;

        return (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? Unmatched;
    }
}
