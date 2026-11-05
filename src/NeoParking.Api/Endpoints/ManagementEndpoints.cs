namespace NeoParking.Api.Endpoints;

using System.Security.Claims;
using NeoParking.Api.Extensions;
using NeoParking.Management.Application;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Primitives;

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
            // Recusa esperada: tratada aqui porque o handler global registra toda exceção com stack trace.
            try
            {
                var user  = await service.LoginAsync(request);
                var token = tokenGenerator.Generate(user);
                return Results.Ok(new { token });
            }
            catch (InvalidCredentialsException ex)
            {
                return Results.Json(
                    new { status = StatusCodes.Status401Unauthorized, error = ex.Message },
                    statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .WithName("Login");

        // ── bootstrap: cria o Owner inicial ──────────────────────────────────
        group.MapPost("/owner", async (CreateOwnerRequest request, OperatorService service) =>
        {
            var result = await service.CreateOwnerAsync(request);
            return Results.Created($"/operators/{result.Id}", result);
        })
        .WithName("CreateOwner");

        // O solicitante vem sempre do token. As policies filtram pelo papel do token;
        // o OperatorService confere papel e estado no banco (decisão D3).

        // ── criar operator / admin ────────────────────────────────────────────
        group.MapPost("/", async (
            CreateOperatorRequest request,
            ClaimsPrincipal user,
            OperatorService service) =>
        {
            var result = await service.CreateOperatorAsync(user.GetOperatorId(), request);
            return Results.Created($"/operators/{result.Id}", result);
        })
        .WithName("CreateOperator")
        .RequireAuthorization(AuthorizationPolicies.OwnerOrAdmin);

        // ── consulta ─────────────────────────────────────────────────────────
        group.MapGet("/me", async (ClaimsPrincipal user, OperatorService service) =>
        {
            var result = await service.GetCurrentAsync(user.GetOperatorId());
            return Results.Ok(result);
        })
        .WithName("GetCurrentOperator")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        group.MapPost("/me/password", async (
            UpdatePasswordRequest request, ClaimsPrincipal user, OperatorService service) =>
        {
            var result = await service.ChangePasswordAsync(user.GetOperatorId(), request);
            return result switch
            {
                ChangePasswordResult.Changed => Results.NoContent(),
                ChangePasswordResult.WrongCurrentPassword => Rejected("The current password is incorrect."),
                _ => Rejected("The new password is required.")
            };
        })
        .WithName("ChangeOwnPassword")
        .RequireAuthorization(AuthorizationPolicies.AnyOperator);

        group.MapGet("/", async (
            ClaimsPrincipal user, OperatorService service, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
        {
            var result = await service.ListAsync(user.GetOperatorId(), new PageRequest(page, pageSize));
            return Results.Ok(result);
        })
        .WithName("ListOperators")
        .RequireAuthorization(AuthorizationPolicies.OwnerOrAdmin);

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, OperatorService service) =>
        {
            var result = await service.GetByIdAsync(user.GetOperatorId(), id);
            return Results.Ok(result);
        })
        .WithName("GetOperator")
        .RequireAuthorization(AuthorizationPolicies.OwnerOrAdmin);

        // ── promoção / rebaixamento ───────────────────────────────────────────
        group.MapPost("/{targetId:guid}/promote", async (
            Guid targetId, ClaimsPrincipal user, OperatorService service) =>
        {
            await service.PromoteToAdminAsync(user.GetOperatorId(), targetId);
            return Results.NoContent();
        })
        .WithName("PromoteToAdmin")
        .RequireAuthorization(AuthorizationPolicies.Owner);

        group.MapPost("/{targetId:guid}/demote", async (
            Guid targetId, ClaimsPrincipal user, OperatorService service) =>
        {
            await service.DemoteToOperatorAsync(user.GetOperatorId(), targetId);
            return Results.NoContent();
        })
        .WithName("DemoteToOperator")
        .RequireAuthorization(AuthorizationPolicies.Owner);

        // ── ativação / desativação ────────────────────────────────────────────
        group.MapPost("/{targetId:guid}/deactivate", async (
            Guid targetId, ClaimsPrincipal user, OperatorService service) =>
        {
            await service.DeactivateAsync(user.GetOperatorId(), targetId);
            return Results.NoContent();
        })
        .WithName("DeactivateOperator")
        .RequireAuthorization(AuthorizationPolicies.OwnerOrAdmin);

        group.MapPost("/{targetId:guid}/activate", async (
            Guid targetId, ClaimsPrincipal user, OperatorService service) =>
        {
            await service.ActivateAsync(user.GetOperatorId(), targetId);
            return Results.NoContent();
        })
        .WithName("ActivateOperator")
        .RequireAuthorization(AuthorizationPolicies.OwnerOrAdmin);
    }

    private static IResult Rejected(string error) =>
        Results.Json(new { status = StatusCodes.Status422UnprocessableEntity, error }, statusCode: StatusCodes.Status422UnprocessableEntity);
}
