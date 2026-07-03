namespace NeoParking.Api.Extensions;

using NeoParking.Api;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Observability;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Registra tracing (OpenTelemetry), HttpClient com propagação de traceparent,
    /// e as abstrações de correlation id / dispatch de eventos de domínio usadas
    /// por todos os Bounded Contexts.
    /// </summary>
    public static IServiceCollection AddNeoParkingObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: "neoparking-api", serviceVersion: "1.0.0"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        services.AddHttpClient();

        services.AddScoped<ICorrelationIdProvider, HttpCorrelationIdProvider>();
        services.AddScoped<IEventDispatcher, MediatREventDispatcher>();

        return services;
    }
}
