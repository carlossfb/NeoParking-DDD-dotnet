namespace NeoParking.E2E.Tests.Security;

using System.Net;
using FluentAssertions;

[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public class OperatorSecurityTests
{
    private readonly SecurityFixture _f;

    public OperatorSecurityTests(SecurityFixture fixture) => _f = fixture;

    private static object NewOperator(string role = "Operator") => new
    {
        name = "E2E",
        email = SecurityFixture.UniqueEmail(),
        password = SecurityFixture.Password,
        role
    };

    // ── TD-101 ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("promote")]
    [InlineData("demote")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task OldRoutes_WithRequesterIdInPath_ShouldNoLongerExist(string action)
    {
        var target = await _f.CreateActorAsync("Operator");

        var response = await _f.SendAsync(HttpMethod.Post,
            $"/operators/{_f.Owner.Id}/{action}/{target.Id}", _f.Operator.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OldCreateRoute_WithRequesterIdInPath_ShouldNoLongerExist()
    {
        var response = await _f.SendAsync(HttpMethod.Post,
            $"/operators/{_f.Owner.Id}", _f.Owner.Token, NewOperator());

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("promote")]
    [InlineData("demote")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task OperatorToken_ShouldBeForbidden_EvenKnowingTheOwnerId(string action)
    {
        var target = await _f.CreateActorAsync("Operator");

        var response = await _f.SendAsync(HttpMethod.Post,
            $"/operators/{target.Id}/{action}?requesterId={_f.Owner.Id}", _f.Operator.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── TD-104 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeactivatedAdmin_WithValidToken_ShouldBeForbiddenInManagement()
    {
        var admin = await _f.CreateActorAsync("Admin");
        var target = await _f.CreateActorAsync("Operator");
        await _f.DeactivateAsync(admin.Id);

        var create = await _f.SendAsync(HttpMethod.Post, "/operators", admin.Token, NewOperator());
        var deactivate = await _f.SendAsync(HttpMethod.Post, $"/operators/{target.Id}/deactivate", admin.Token);
        var read = await _f.SendAsync(HttpMethod.Get, $"/operators/{target.Id}", admin.Token);

        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deactivate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // D3: em Management o papel vem do banco, não do token.
    [Fact]
    public async Task DemotedAdmin_WithStillValidAdminToken_ShouldBeForbiddenInManagement()
    {
        var admin = await _f.CreateActorAsync("Admin");
        await _f.DemoteAsync(admin.Id);

        var create = await _f.SendAsync(HttpMethod.Post, "/operators", admin.Token, NewOperator());
        var read = await _f.SendAsync(HttpMethod.Get, $"/operators/{_f.Operator.Id}", admin.Token);

        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // D3: fora de Management o token vale até expirar.
    [Fact]
    public async Task DeactivatedOperator_WithValidToken_ShouldStillReachAccess_AsAcceptedInD3()
    {
        var op = await _f.CreateActorAsync("Operator");
        await _f.DeactivateAsync(op.Id);

        var response = await _f.SendAsync(HttpMethod.Get, "/access/tickets", op.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── TD-105 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminCreatingAdmin_ShouldBeForbidden()
    {
        var response = await _f.SendAsync(HttpMethod.Post, "/operators", _f.Admin.Token, NewOperator("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminCreatingOwner_ShouldBeForbidden()
    {
        var response = await _f.SendAsync(HttpMethod.Post, "/operators", _f.Admin.Token, NewOperator("Owner"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BusinessRuleViolation_ShouldStillReturn422()
    {
        var invalidRole = await _f.SendAsync(HttpMethod.Post, "/operators", _f.Owner.Token, NewOperator("Janitor"));
        var secondOwner = await _f.SendAsync(HttpMethod.Post, "/operators", _f.Owner.Token, NewOperator("Owner"));
        var invalidCpf = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Operator.Token,
            new { name = "Cliente", phoneNumber = "+5511999999999", type = "Individual", document = "00000000000" });

        invalidRole.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        secondOwner.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        invalidCpf.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── TD-105 e TD-106 ──────────────────────────────────────────────────────

    [Fact]
    public async Task Login_UnknownEmail_WrongPassword_AndInactiveAccount_ShouldBeIndistinguishable()
    {
        var inactive = await _f.CreateActorAsync("Operator");
        await _f.DeactivateAsync(inactive.Id);

        var unknown = await LoginAsync(SecurityFixture.UniqueEmail(), SecurityFixture.Password);
        var wrongPassword = await LoginAsync(_f.Operator.Email, "wrong-password");
        var inactiveRightPassword = await LoginAsync(inactive.Email, SecurityFixture.Password);
        var inactiveWrongPassword = await LoginAsync(inactive.Email, "wrong-password");

        unknown.Status.Should().Be(HttpStatusCode.Unauthorized);
        wrongPassword.Should().Be(unknown);
        inactiveRightPassword.Should().Be(unknown);
        inactiveWrongPassword.Should().Be(unknown);
    }

    private async Task<(HttpStatusCode Status, string Body)> LoginAsync(string email, string password)
    {
        var response = await _f.SendAsync(HttpMethod.Post, "/operators/login", body: new { email, password });
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    // ── TD-107 ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task Admin_ShouldNotActivateAdminOrOwner(string targetRole)
    {
        Guid targetId;
        if (targetRole == "Owner")
        {
            targetId = _f.Owner.Id;
        }
        else
        {
            var target = await _f.CreateActorAsync("Admin");
            await _f.DeactivateAsync(target.Id);
            targetId = target.Id;
        }

        var response = await _f.SendAsync(HttpMethod.Post, $"/operators/{targetId}/activate", _f.Admin.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_ShouldActivateOperator()
    {
        var target = await _f.CreateActorAsync("Operator");
        await _f.DeactivateAsync(target.Id);

        var response = await _f.SendAsync(HttpMethod.Post, $"/operators/{target.Id}/activate", _f.Admin.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
