namespace NeoParking.Api.Endpoints;

using System.Security.Claims;
using NeoParking.Access.Application;
using NeoParking.Api.Extensions;

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

        group.MapGet("/", async (IClientService service) =>
        {
            var result = await service.GetAllClientsAsync();
            return Results.Ok(result);
        })
        .WithName("GetAllClients")
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