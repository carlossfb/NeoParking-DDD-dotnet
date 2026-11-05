namespace NeoParking.E2E.Tests.Privacy;

using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-709: recusa esperada não vira erro com stack trace, e o e-mail não vai para a mensagem de conflito.</summary>
[Collection(SecurityCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ExpectedFailureLogTests
{
    private const string ExceptionHandlerError = "Error Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";

    private readonly SecurityFixture _f;

    public ExpectedFailureLogTests(SecurityFixture fixture) => _f = fixture;

    [Fact]
    public async Task RejectedLogin_ShouldNotLogAnErrorNorTheEmail()
    {
        var email = SecurityFixture.UniqueEmail();
        var before = _f.Logs.Text.Length;

        var unknown = await _f.SendAsync(HttpMethod.Post, "/operators/login", body: new { email, password = "wrong" });
        var wrongPassword = await _f.SendAsync(HttpMethod.Post, "/operators/login", body: new { email = _f.Owner.Email, password = "wrong" });

        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await unknown.Content.ReadAsStringAsync();
        body.Should().Be(await wrongPassword.Content.ReadAsStringAsync()).And.Contain("Invalid credentials.").And.Contain("401");

        var logged = _f.Logs.Text[before..];
        logged.Should().NotContain(ExceptionHandlerError).And.NotContain(nameof(Shared.Kernel.Exceptions.InvalidCredentialsException))
            .And.NotContain(email).And.NotContain(_f.Owner.Email);
    }

    [Fact]
    public async Task EmailInUse_ShouldNotCarryTheEmail()
    {
        var before = _f.Logs.Text.Length;

        var response = await _f.SendAsync(HttpMethod.Post, "/operators", _f.Owner.Token,
            new { name = "Outro", email = _f.Admin.Email, password = SecurityFixture.Password, role = "Operator" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(_f.Admin.Email);
        _f.Logs.Text[before..].Should().NotContain(_f.Admin.Email);
    }

    [Fact]
    public async Task UnexpectedError_ShouldKeepTheFullLog()
    {
        var created = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Owner.Token,
            new { name = "Cliente", phoneNumber = "+5511999999999", type = "Individual", document = SecurityFixture.UniqueCpf() });
        var client = await _f.ReadAsync<SecurityFixture.IdResponse>(created);
        await _f.ExecuteAccessSqlAsync($"UPDATE clients SET document_encrypted = 'v1.AAAA' WHERE Id = '{client.Id}'");
        var before = _f.Logs.Text.Length;

        try
        {
            var response = await _f.SendAsync(HttpMethod.Get, $"/clients/{client.Id}", _f.Owner.Token);

            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            var logged = _f.Logs.Text[before..];
            logged.Should().Contain(ExceptionHandlerError).And.Contain(nameof(InvalidOperationException)).And.Contain("   at ");
        }
        finally
        {
            // Não deixa uma linha ilegível para os testes que listam clientes.
            await _f.ExecuteAccessSqlAsync($"DELETE FROM clients WHERE Id = '{client.Id}'");
        }
    }
}
