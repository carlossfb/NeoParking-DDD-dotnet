using main.common.dto;
using main.domain.ports;

namespace Neoparking.Endpoints
{
    public static class ClientEndpoints
    {
        public static void MapClientEndpoints(this WebApplication app)
        {
            app.MapPost("/clients", async (ClientRequestDTO request, IClientService service) =>
            {
                var result = await service.CreateClientAsync(request);
                return Results.Created($"/clients/{result.Id}", result);
            })
            .WithName("CreateClient")
            .WithOpenApi();

            app.MapGet("/clients/{id}", async (Guid id, IClientService service) =>
            {
                var result = await service.GetClientByIdAsync(id);
                return result != null ? Results.Ok(result) : Results.NotFound();
            })
            .WithName("GetClient")
            .WithOpenApi();
            
            app.MapDelete("/clients/{id}", async (Guid id, IClientService service) =>
            {
                await service.DeleteClientAsync(id);
                return Results.NoContent();
            })
            .WithName("DeleteClient")
            .WithOpenApi();

            app.MapGet("/clients",  async (IClientService service) =>
            {
                var result = await service.GetAllClientsAsync();
                return Results.Ok(result);
            })
            .WithName("GetAllClients")
            .WithOpenApi();

            app.MapPatch("/clients/{id}", async (Guid id, ClientUpdateDTO request, IClientService service) =>
            {
                var result = await service.UpdateClientAsync(id, request);
                return Results.Ok(result);
            })
            .WithName("UpdateClient")
            .WithOpenApi();
        }
    }
}