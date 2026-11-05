namespace NeoParking.Api.Endpoints;

using System.Security.Claims;
using NeoParking.Access.Application;
using NeoParking.Api.Extensions;
using NeoParking.Shared.Kernel.Primitives;

public static class ClientEndpoints
{
    public static void MapClientEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/clients").WithOpenApi();

        group.MapPost("/", async (ClientRequestDTO request, IClientService service) =>
        {
            var result = await service.CreateClientAsync(request);
            return Results.Created($"/clients/{result.Id}", result);
        })
        .WithName("CreateClient")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        group.MapGet("/{id:guid}", async (Guid id, IClientService service) =>
        {
            var result = await service.GetClientByIdAsync(id);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetClient")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        // O documento completo só sai aqui. A resposta não fica em cache, e cada leitura é auditada.
        group.MapGet("/{id:guid}/document", async (Guid id, ClaimsPrincipal user, HttpContext http, IClientService service) =>
        {
            var result = await service.RevealDocumentAsync(id, user.GetOperatorId());
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(result);
        })
        .WithName("GetClientDocument")
        .RequireAuthorization(AuthorizationPolicies.OwnerOrAdmin);

        // Só o Owner: o Admin também lê documentos e não deve auditar a si mesmo.
        group.MapGet("/{id:guid}/document-accesses", async (
            Guid id, IClientService service, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
        {
            var result = await service.ListDocumentAccessesAsync(id, new PageRequest(page, pageSize));
            return Results.Ok(result);
        })
        .WithName("ListClientDocumentAccesses")
        .RequireAuthorization(AuthorizationPolicies.Owner);

        group.MapGet("/", async (IClientService service, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
        {
            var result = await service.ListClientsAsync(new PageRequest(page, pageSize));
            return Results.Ok(result);
        })
        .WithName("GetAllClients")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        // POST para que placa e documento fiquem no corpo, fora de URLs, logs e traces.
        group.MapPost("/search", async (ClientSearchRequest request, IClientService service) =>
        {
            var result = await service.SearchClientAsync(request);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("SearchClient")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        group.MapPatch("/{id:guid}", async (Guid id, ClientUpdateDTO request, IClientService service) =>
        {
            var result = await service.UpdateClientAsync(id, request);
            return Results.Ok(result);
        })
        .WithName("UpdateClient")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        group.MapDelete("/{id:guid}", async (Guid id, IClientService service) =>
        {
            await service.DeleteClientAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteClient")
        .RequireAuthorization(AuthorizationPolicies.OwnerOrAdmin);

        group.MapPost("/{id:guid}/vehicles", async (Guid id, VehicleRequestDTO request, IClientService service) =>
        {
            var result = await service.RegisterVehicleAsync(id, request);
            return Results.Created($"/clients/{id}/vehicles/{result.Id}", result);
        })
        .WithName("RegisterVehicle")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        group.MapDelete("/{id:guid}/vehicles/{vehicleId:guid}", async (Guid id, Guid vehicleId, IClientService service) =>
        {
            await service.RemoveVehicleAsync(id, vehicleId);
            return Results.NoContent();
        })
        .WithName("RemoveVehicle")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);
    }
}