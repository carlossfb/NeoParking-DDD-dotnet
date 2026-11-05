namespace NeoParking.E2E.Tests.Privacy;

using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-604: o documento não fica legível no banco, e a unicidade continua valendo.</summary>
[Collection(SecurityCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DocumentEncryptionTests
{
    private readonly SecurityFixture _f;

    public DocumentEncryptionTests(SecurityFixture fixture) => _f = fixture;

    private Task<HttpResponseMessage> CreateAsync(string type, string document) => _f.SendAsync(
        HttpMethod.Post, "/clients", _f.Owner.Token,
        new { name = "Cliente", phoneNumber = "+5511999999999", type, document });

    [Theory]
    [InlineData("Individual")]
    [InlineData("Company")]
    public async Task Document_ShouldNotBeLegibleInAnyColumn_AndStillBeRevealed(string type)
    {
        var document = type == "Company" ? SecurityFixture.UniqueCnpj() : SecurityFixture.UniqueCpf();

        var created = await _f.ReadAsync<SecurityFixture.IdResponse>(await CreateAsync(type, document));

        var row = await _f.ReadClientRowAsync(created.Id);
        row.Should().NotContain(document);
        row.Should().NotContain(document[3..9]);

        var revealed = await _f.SendAsync(HttpMethod.Get, $"/clients/{created.Id}/document", _f.Owner.Token);
        (await _f.ReadAsync<RevealedDocument>(revealed)).Document.Should().Be(document);
    }

    [Fact]
    public async Task SameDocument_ShouldStillBeAConflict()
    {
        var cpf = SecurityFixture.UniqueCpf();
        await CreateAsync("Individual", cpf);

        var response = await CreateAsync("Individual", cpf);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdatingToADocumentInUse_ShouldBeAConflict_AndKeepTheCurrentOne()
    {
        var taken = SecurityFixture.UniqueCpf();
        var own = SecurityFixture.UniqueCpf();
        await CreateAsync("Individual", taken);
        var client = await _f.ReadAsync<SecurityFixture.IdResponse>(await CreateAsync("Individual", own));

        var response = await _f.SendAsync(HttpMethod.Patch, $"/clients/{client.Id}", _f.Owner.Token, new { document = taken });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var revealed = await _f.SendAsync(HttpMethod.Get, $"/clients/{client.Id}/document", _f.Owner.Token);
        (await _f.ReadAsync<RevealedDocument>(revealed)).Document.Should().Be(own);
    }

    private sealed record RevealedDocument(string Type, string Document);
}
