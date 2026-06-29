namespace NeoParking.Api.Endpoints;

using NeoParking.Management.Application;
using NeoParking.Management.Infrastructure;

public static class ManagementEndpoints
{
    public static void MapManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/operators").WithOpenApi();

        // ── autenticação ──────────────────────────────────────────────────────
        group.MapPost("/login", async (
            LoginRequest request,
            OperatorService service,
            IJwtTokenGenerator tokenGenerator) =>
        {
            var user  = await service.LoginAsync(request);
            var token = tokenGenerator.Generate(user);
            return Results.Ok(new { token });
        })
        .WithName("Login");

        // ── bootstrap: cria o Owner inicial ──────────────────────────────────
        group.MapPost("/owner", async (CreateOwnerRequest request, OperatorService service) =>
        {
            var result = await service.CreateOwnerAsync(request);
            return Results.Created($"/operators/{result.Id}", result);
        })
        .WithName("CreateOwner");

        // ── criar operator / admin ────────────────────────────────────────────
        group.MapPost("/{requesterId:guid}", async (
            Guid requesterId,
            CreateOperatorRequest request,
            OperatorService service) =>
        {
            var result = await service.CreateOperatorAsync(requesterId, request);
            return Results.Created($"/operators/{result.Id}", result);
        })
        .WithName("CreateOperator");

        // ── consulta ─────────────────────────────────────────────────────────
        group.MapGet("/{id:guid}", async (Guid id, OperatorService service) =>
        {
            var result = await service.GetByIdAsync(id);
            return Results.Ok(result);
        })
        .WithName("GetOperator");

        // ── promoção / rebaixamento ───────────────────────────────────────────
        group.MapPost("/{requesterId:guid}/promote/{targetId:guid}", async (
            Guid requesterId, Guid targetId, OperatorService service) =>
        {
            await service.PromoteToAdminAsync(requesterId, targetId);
            return Results.NoContent();
        })
        .WithName("PromoteToAdmin");

        group.MapPost("/{requesterId:guid}/demote/{targetId:guid}", async (
            Guid requesterId, Guid targetId, OperatorService service) =>
        {
            await service.DemoteToOperatorAsync(requesterId, targetId);
            return Results.NoContent();
        })
        .WithName("DemoteToOperator");

        // ── ativação / desativação ────────────────────────────────────────────
        group.MapPost("/{requesterId:guid}/deactivate/{targetId:guid}", async (
            Guid requesterId, Guid targetId, OperatorService service) =>
        {
            await service.DeactivateAsync(requesterId, targetId);
            return Results.NoContent();
        })
        .WithName("DeactivateOperator");

        group.MapPost("/{requesterId:guid}/activate/{targetId:guid}", async (
            Guid requesterId, Guid targetId, OperatorService service) =>
        {
            await service.ActivateAsync(requesterId, targetId);
            return Results.NoContent();
        })
        .WithName("ActivateOperator");
    }
}
