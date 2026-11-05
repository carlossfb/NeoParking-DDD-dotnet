namespace NeoParking.E2E.Tests.ViewRoutes;

using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-806: o operador troca a própria senha, informando a atual.</summary>
[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public sealed class ChangePasswordTests
{
    private const string Route = "/operators/me/password";
    private const string NewPassword = "E2e-New-Password-456!";

    private readonly SecurityFixture _f;

    public ChangePasswordTests(SecurityFixture fixture) => _f = fixture;

    [Fact]
    public async Task Change_ShouldReplaceThePassword()
    {
        var actor = await _f.CreateActorAsync("Operator");

        var response = await _f.SendAsync(HttpMethod.Post, Route, actor.Token,
            new { currentPassword = SecurityFixture.Password, newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LoginAsync(actor.Email, SecurityFixture.Password)).Should().Be(HttpStatusCode.Unauthorized);
        (await LoginAsync(actor.Email, NewPassword)).Should().Be(HttpStatusCode.OK);
    }

    // D3: o token não guarda estado, e a troca não cria revogação. Ele vale até expirar.
    [Fact]
    public async Task TokenIssuedBeforeTheChange_ShouldKeepWorkingUntilItExpires()
    {
        var actor = await _f.CreateActorAsync("Operator");
        await _f.SendAsync(HttpMethod.Post, Route, actor.Token,
            new { currentPassword = SecurityFixture.Password, newPassword = NewPassword });

        var response = await _f.SendAsync(HttpMethod.Get, "/operators/me", actor.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("wrong-password", NewPassword)]
    [InlineData("", NewPassword)]
    [InlineData(null, NewPassword)]
    [InlineData(SecurityFixture.Password, "   ")]
    [InlineData(SecurityFixture.Password, null)]
    public async Task Rejected_ShouldKeepThePassword_WithoutStackTraceOrEmailInTheLogs(string? current, string? replacement)
    {
        var actor = await _f.CreateActorAsync("Operator");
        var logged = _f.Logs.Text.Length;

        var response = await _f.SendAsync(HttpMethod.Post, Route, actor.Token,
            new { currentPassword = current, newPassword = replacement });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LoginAsync(actor.Email, SecurityFixture.Password)).Should().Be(HttpStatusCode.OK);
        _f.Logs.Text[logged..].Should().NotContain("ExceptionHandlerMiddleware");
        _f.Logs.Text.Should().NotContain(actor.Email);
    }

    private async Task<HttpStatusCode> LoginAsync(string email, string password) =>
        (await _f.SendAsync(HttpMethod.Post, "/operators/login", body: new { email, password })).StatusCode;
}
