namespace NeoParking.Api.Endpoints;

using System.Security.Claims;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Api.Extensions;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        // ── tickets (entrada/saída na cancela) ───────────────────────────────────
        var tickets = app.MapGroup("/access/tickets").WithOpenApi();

        tickets.MapPost("/", async (RegisterEntryRequest request, ITicketService service) =>
        {
            var result = await service.RegisterEntryAsync(request);
            return Results.Created($"/access/tickets/{result.Id}", result);
        })
        .WithName("RegisterTicketEntry")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        tickets.MapPost("/{id:guid}/exit", async (
            Guid id, RegisterExitRequest request, ITicketService service) =>
        {
            var result = await service.RegisterExitAsync(id, request);
            return Results.Ok(result);
        })
        .WithName("RegisterTicketExit")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        tickets.MapGet("/by-code/{code}", async (string code, ITicketService service) =>
        {
            var result = await service.GetByCodeAsync(code);
            return Results.Ok(result);
        })
        .WithName("GetTicketByCode")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        tickets.MapPost("/by-code/{code}/exit", async (
            string code, RegisterExitRequest request, ITicketService service) =>
        {
            var result = await service.RegisterExitByCodeAsync(code, request);
            return Results.Ok(result);
        })
        .WithName("RegisterTicketExitByCode")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        // Para ticket perdido: o motivo vem do corpo, e o autor, do token.
        tickets.MapPost("/by-plate/{plate}/exit", async (
            string plate, RegisterExitByPlateRequest request, ClaimsPrincipal user, ITicketService service) =>
        {
            var result = await service.RegisterExitByPlateAsync(plate, request, user.GetOperatorId());
            return Results.Ok(result);
        })
        .WithName("RegisterTicketExitByPlate")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        tickets.MapGet("/{id:guid}", async (Guid id, ITicketService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return Results.Ok(result);
        })
        .WithName("GetTicket")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        // Listagem geral (abertos e fechados), filtrável por status/placa/período de entrada.
        tickets.MapGet("/", async (
            TicketStatus? status, string? plate, DateTime? entryFrom, DateTime? entryTo,
            ITicketService service, int page = 1, int pageSize = 20) =>
        {
            var request = new ListTicketsRequest(status, plate, entryFrom, entryTo, page, pageSize);
            var result = await service.ListAsync(request);
            return Results.Ok(result);
        })
        .WithName("ListTickets")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);
    }
}
