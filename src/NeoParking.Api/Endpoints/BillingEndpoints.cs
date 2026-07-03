namespace NeoParking.Api.Endpoints;

using NeoParking.Billing.Application;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app)
    {
        // ── política de tarifação (Owner) ────────────────────────────────────────
        var pricing = app.MapGroup("/billing/pricing-policies").WithOpenApi();

        pricing.MapPost("/", async (DefinePricingPolicyRequest request, PricingPolicyService service) =>
        {
            var result = await service.DefineAsync(request);
            return Results.Created($"/billing/pricing-policies/{result.Id}", result);
        })
        .WithName("DefinePricingPolicy")
        .RequireAuthorization();

        pricing.MapGet("/active", async (PricingPolicyService service) =>
        {
            var result = await service.GetActiveAsync();
            return Results.Ok(result);
        })
        .WithName("GetActivePricingPolicy")
        .RequireAuthorization();

        pricing.MapGet("/history", async (PricingPolicyService service) =>
        {
            var result = await service.GetHistoryAsync();
            return Results.Ok(result);
        })
        .WithName("GetPricingPolicyHistory")
        .RequireAuthorization();

        pricing.MapGet("/effective-at", async (DateTime at, PricingPolicyService service) =>
        {
            var result = await service.GetEffectiveAtAsync(at);
            return Results.Ok(result);
        })
        .WithName("GetPricingPolicyEffectiveAt")
        .RequireAuthorization();

        // ── tickets (somente consulta — entrada/saída são registradas no Access) ─
        var tickets = app.MapGroup("/billing/tickets").WithOpenApi();

        // Rota específica antes da parametrizada — evita qualquer ambiguidade de matching.
        tickets.MapGet("/fee-history", async (
            DateTime? from, DateTime? to, TicketService service) =>
        {
            var result = await service.GetFeeHistoryAsync(from, to);
            return Results.Ok(result);
        })
        .WithName("GetFeeHistory")
        .RequireAuthorization();

        tickets.MapGet("/{id:guid}", async (Guid id, TicketService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return Results.Ok(result);
        })
        .WithName("GetBillingTicket")
        .RequireAuthorization();
    }
}
