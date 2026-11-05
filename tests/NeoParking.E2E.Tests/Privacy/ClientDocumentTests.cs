namespace NeoParking.E2E.Tests.Privacy;

using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-602: o cliente tem tipo e documento, e o CNPJ pode ser alfanumérico.</summary>
[Collection(SecurityCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ClientDocumentTests
{
    private readonly SecurityFixture _f;

    public ClientDocumentTests(SecurityFixture fixture) => _f = fixture;

    private Task<HttpResponseMessage> CreateAsync(string type, string document) => _f.SendAsync(
        HttpMethod.Post, "/clients", _f.Operator.Token,
        new { name = "Cliente", phoneNumber = "+5511999999999", type, document });

    [Fact]
    public async Task Company_WithAnAlphanumericCnpj_ShouldBeCreated()
    {
        var response = await CreateAsync("Company", SecurityFixture.UniqueCnpj());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await _f.ReadAsync<ClientView>(response)).Type.Should().Be("Company");
    }

    [Fact]
    public async Task Individual_WithACnpj_ShouldBeRejected()
    {
        var response = await CreateAsync("Individual", SecurityFixture.UniqueCnpj());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UnknownType_ShouldBeRejected()
    {
        var response = await CreateAsync("Foreigner", SecurityFixture.UniqueCpf());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task OldCpfField_ShouldNoLongerBeAccepted()
    {
        var response = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Operator.Token,
            new { name = "Cliente", phoneNumber = "+5511999999999", cpf = SecurityFixture.UniqueCpf() });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SameCnpj_ShouldBeAConflict()
    {
        var cnpj = SecurityFixture.UniqueCnpj();
        await CreateAsync("Company", cnpj);

        var response = await CreateAsync("Company", cnpj);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChangingTheType_RequiresADocumentOfThatType()
    {
        var created = await _f.ReadAsync<ClientView>(await CreateAsync("Individual", SecurityFixture.UniqueCpf()));

        var withoutDocument = await _f.SendAsync(HttpMethod.Patch, $"/clients/{created.Id}", _f.Operator.Token,
            new { type = "Company" });
        var withDocument = await _f.SendAsync(HttpMethod.Patch, $"/clients/{created.Id}", _f.Operator.Token,
            new { type = "Company", document = SecurityFixture.UniqueCnpj() });

        withoutDocument.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await _f.ReadAsync<ClientView>(withDocument)).Type.Should().Be("Company");
    }

    private sealed record ClientView(Guid Id, string Type, string Document);
}
