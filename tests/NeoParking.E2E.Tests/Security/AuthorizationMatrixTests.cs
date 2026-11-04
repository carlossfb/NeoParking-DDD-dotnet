namespace NeoParking.E2E.Tests.Security;

using System.Net;
using FluentAssertions;

/// <summary>
/// TD-103: cada célula da matriz D2 (technical-debt-remediation.md, seção 3).
/// Sem token 401, papel insuficiente 403, papel suficiente com o código de sucesso do endpoint.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public class AuthorizationMatrixTests
{
    private enum Minimum { AnyOperator, OwnerOrAdmin, Owner }

    private sealed record Call(HttpMethod Method, string Path, object? Body = null);

    private sealed record Row(Minimum Required, HttpStatusCode Success, Func<SecurityFixture, Task<Call>> Build);

    private static Call Get(string path) => new(HttpMethod.Get, path);
    private static Call Post(string path, object? body = null) => new(HttpMethod.Post, path, body);
    private static Call Delete(string path) => new(HttpMethod.Delete, path);

    private static readonly Dictionary<string, Row> Rows = new()
    {
        ["POST /operators"] = new(Minimum.OwnerOrAdmin, HttpStatusCode.Created, _ => Task.FromResult(Post("/operators",
            new { name = "E2E", email = SecurityFixture.UniqueEmail(), password = SecurityFixture.Password, role = "Operator" }))),
        ["GET /operators/{id}"] = new(Minimum.OwnerOrAdmin, HttpStatusCode.OK, f => Task.FromResult(Get($"/operators/{f.Operator.Id}"))),
        ["POST /operators/{id}/promote"] = new(Minimum.Owner, HttpStatusCode.NoContent, async f =>
        {
            var target = await f.CreateActorAsync("Operator");
            return Post($"/operators/{target.Id}/promote");
        }),
        ["POST /operators/{id}/demote"] = new(Minimum.Owner, HttpStatusCode.NoContent, async f =>
        {
            var target = await f.CreateActorAsync("Admin");
            return Post($"/operators/{target.Id}/demote");
        }),
        ["POST /operators/{id}/activate"] = new(Minimum.OwnerOrAdmin, HttpStatusCode.NoContent, async f =>
        {
            var target = await f.CreateActorAsync("Operator");
            await f.DeactivateAsync(target.Id);
            return Post($"/operators/{target.Id}/activate");
        }),
        ["POST /operators/{id}/deactivate"] = new(Minimum.OwnerOrAdmin, HttpStatusCode.NoContent, async f =>
        {
            var target = await f.CreateActorAsync("Operator");
            return Post($"/operators/{target.Id}/deactivate");
        }),

        ["POST /clients"] = new(Minimum.AnyOperator, HttpStatusCode.Created, _ => Task.FromResult(Post("/clients", NewClient()))),
        ["GET /clients"] = new(Minimum.AnyOperator, HttpStatusCode.OK, _ => Task.FromResult(Get("/clients"))),
        ["GET /clients/{id}"] = new(Minimum.AnyOperator, HttpStatusCode.OK, async f => Get($"/clients/{await SeedClientAsync(f)}")),
        ["PATCH /clients/{id}"] = new(Minimum.AnyOperator, HttpStatusCode.OK, async f =>
            new Call(HttpMethod.Patch, $"/clients/{await SeedClientAsync(f)}", new { name = "Atualizado" })),
        ["POST /clients/{id}/vehicles"] = new(Minimum.AnyOperator, HttpStatusCode.Created, async f =>
            Post($"/clients/{await SeedClientAsync(f)}/vehicles", new { plate = SecurityFixture.UniquePlate() })),
        ["DELETE /clients/{id}/vehicles/{id}"] = new(Minimum.AnyOperator, HttpStatusCode.NoContent, async f =>
        {
            var clientId = await SeedClientAsync(f);
            var vehicle = await f.ReadAsync<SecurityFixture.IdResponse>(await f.SendAsync(
                HttpMethod.Post, $"/clients/{clientId}/vehicles", f.Owner.Token, new { plate = SecurityFixture.UniquePlate() }));
            return Delete($"/clients/{clientId}/vehicles/{vehicle.Id}");
        }),
        ["DELETE /clients/{id}"] = new(Minimum.OwnerOrAdmin, HttpStatusCode.NoContent, async f => Delete($"/clients/{await SeedClientAsync(f)}")),

        ["POST /access/tickets"] = new(Minimum.AnyOperator, HttpStatusCode.Created, _ =>
            Task.FromResult(Post("/access/tickets", new { plate = SecurityFixture.UniquePlate() }))),
        ["POST /access/tickets/{id}/exit"] = new(Minimum.AnyOperator, HttpStatusCode.OK, async f =>
            Post($"/access/tickets/{await SeedTicketAsync(f)}/exit", new { })),
        ["GET /access/tickets"] = new(Minimum.AnyOperator, HttpStatusCode.OK, _ => Task.FromResult(Get("/access/tickets"))),
        ["GET /access/tickets/{id}"] = new(Minimum.AnyOperator, HttpStatusCode.OK, async f => Get($"/access/tickets/{await SeedTicketAsync(f)}")),

        ["GET /billing/pricing-policies/active"] = new(Minimum.AnyOperator, HttpStatusCode.OK, _ =>
            Task.FromResult(Get("/billing/pricing-policies/active"))),
        ["GET /billing/pricing-policies/history"] = new(Minimum.OwnerOrAdmin, HttpStatusCode.OK, _ =>
            Task.FromResult(Get("/billing/pricing-policies/history"))),
        ["GET /billing/pricing-policies/effective-at"] = new(Minimum.OwnerOrAdmin, HttpStatusCode.OK, _ =>
            Task.FromResult(Get("/billing/pricing-policies/effective-at?at=2026-01-01T10:00:00Z"))),
        ["POST /billing/pricing-policies"] = new(Minimum.Owner, HttpStatusCode.Created, _ =>
            Task.FromResult(Post("/billing/pricing-policies", new { minimumFee = 10m, hourlyRate = 4m }))),
        ["GET /billing/tickets/fee-history"] = new(Minimum.OwnerOrAdmin, HttpStatusCode.OK, _ =>
            Task.FromResult(Get("/billing/tickets/fee-history"))),
        ["GET /billing/tickets/{id}"] = new(Minimum.AnyOperator, HttpStatusCode.OK, f =>
            Task.FromResult(Get($"/billing/tickets/{f.BillingTicketId}"))),
    };

    public static IEnumerable<object[]> Cells =>
        from endpoint in Rows.Keys
        from actor in new[] { "Anonymous", "Operator", "Admin", "Owner" }
        select new object[] { endpoint, actor };

    private readonly SecurityFixture _fixture;

    public AuthorizationMatrixTests(SecurityFixture fixture) => _fixture = fixture;

    [Theory]
    [MemberData(nameof(Cells))]
    public async Task Endpoint_ShouldFollowMatrix(string endpoint, string actor)
    {
        var row = Rows[endpoint];
        var call = await row.Build(_fixture);

        var response = await _fixture.SendAsync(call.Method, call.Path, TokenOf(actor), call.Body);

        response.StatusCode.Should().Be(Expected(row, actor));
    }

    [Fact]
    public async Task Login_ShouldBePublic()
    {
        var response = await _fixture.SendAsync(HttpMethod.Post, "/operators/login",
            body: new { email = _fixture.Owner.Email, password = SecurityFixture.Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OwnerBootstrap_ShouldBePublic_AndRejectASecondOwnerAsConflict()
    {
        var response = await _fixture.SendAsync(HttpMethod.Post, "/operators/owner",
            body: new { name = "Other", email = SecurityFixture.UniqueEmail(), password = SecurityFixture.Password });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static HttpStatusCode Expected(Row row, string actor)
    {
        if (actor == "Anonymous")
            return HttpStatusCode.Unauthorized;

        var allowed = row.Required switch
        {
            Minimum.AnyOperator => true,
            Minimum.OwnerOrAdmin => actor is "Admin" or "Owner",
            _ => actor == "Owner"
        };

        return allowed ? row.Success : HttpStatusCode.Forbidden;
    }

    private string? TokenOf(string actor) => actor switch
    {
        "Operator" => _fixture.Operator.Token,
        "Admin" => _fixture.Admin.Token,
        "Owner" => _fixture.Owner.Token,
        _ => null
    };

    private static object NewClient() => new
    {
        name = "Cliente E2E",
        phoneNumber = "+5511999999999",
        cpf = SecurityFixture.UniqueCpf()
    };

    private static async Task<Guid> SeedClientAsync(SecurityFixture f)
    {
        var response = await f.SendAsync(HttpMethod.Post, "/clients", f.Owner.Token, NewClient());
        return (await f.ReadAsync<SecurityFixture.IdResponse>(response)).Id;
    }

    private static async Task<Guid> SeedTicketAsync(SecurityFixture f)
    {
        var response = await f.SendAsync(HttpMethod.Post, "/access/tickets", f.Owner.Token,
            new { plate = SecurityFixture.UniquePlate() });
        return (await f.ReadAsync<SecurityFixture.IdResponse>(response)).Id;
    }
}
