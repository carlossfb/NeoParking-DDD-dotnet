namespace NeoParking.Api.Endpoints;

using NeoParking.Access.Application;

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
        .RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, IClientService service) =>
        {
            var result = await service.GetClientByIdAsync(id);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetClient")
        .RequireAuthorization();

        group.MapGet("/", async (IClientService service) =>
        {
            var result = await service.GetAllClientsAsync();
            return Results.Ok(result);
        })
        .WithName("GetAllClients")
        .RequireAuthorization();

        group.MapPatch("/{id:guid}", async (Guid id, ClientUpdateDTO request, IClientService service) =>
        {
            var result = await service.UpdateClientAsync(id, request);
            return Results.Ok(result);
        })
        .WithName("UpdateClient")
        .RequireAuthorization();

        group.MapDelete("/{id:guid}", async (Guid id, IClientService service) =>
        {
            await service.DeleteClientAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteClient")
        .RequireAuthorization();

        group.MapPost("/{id:guid}/vehicles", async (Guid id, VehicleRequestDTO request, IClientService service) =>
        {
            var result = await service.RegisterVehicleAsync(id, request);
            return Results.Created($"/clients/{id}/vehicles/{result.Id}", result);
        })
        .WithName("RegisterVehicle")
        .RequireAuthorization();

        group.MapDelete("/{id:guid}/vehicles/{vehicleId:guid}", async (Guid id, Guid vehicleId, IClientService service) =>
        {
            await service.RemoveVehicleAsync(id, vehicleId);
            return Results.NoContent();
        })
        .WithName("RemoveVehicle")
        .RequireAuthorization();
    }
}