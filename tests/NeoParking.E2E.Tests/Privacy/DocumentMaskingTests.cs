namespace NeoParking.E2E.Tests.Privacy;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-603: o documento sai mascarado por padrão; o valor completo tem rota própria e auditoria.</summary>
[Collection(SecurityCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DocumentMaskingTests
{
    private readonly SecurityFixture _f;

    public DocumentMaskingTests(SecurityFixture fixture) => _f = fixture;

    private async Task<(Guid Id, string Cpf, HttpResponseMessage Response)> CreateAsync()
    {
        var cpf = SecurityFixture.UniqueCpf();
        var response = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Owner.Token,
            new { name = "Cliente", phoneNumber = "+5511999999999", type = "Individual", document = cpf });
        var created = await response.Content.ReadFromJsonAsync<ClientView>();
        return (created!.Id, cpf, response);
    }

    private static string MaskOf(string cpf) => $"***.{cpf[3..6]}.{cpf[6..9]}-**";

    [Fact]
    public async Task EveryClientResponse_ShouldMaskTheDocument()
    {
        var (id, cpf, created) = await CreateAsync();
        var mask = MaskOf(cpf);

        (await created.Content.ReadAsStringAsync()).Should().Contain(mask).And.NotContain(cpf);

        var byId = await _f.SendAsync(HttpMethod.Get, $"/clients/{id}", _f.Owner.Token);
        (await byId.Content.ReadAsStringAsync()).Should().Contain(mask).And.NotContain(cpf);

        var all = await _f.SendAsync(HttpMethod.Get, "/clients", _f.Owner.Token);
        (await all.Content.ReadAsStringAsync()).Should().Contain(mask).And.NotContain(cpf);

        var updated = await _f.SendAsync(HttpMethod.Patch, $"/clients/{id}", _f.Owner.Token, new { name = "Atualizado" });
        (await updated.Content.ReadAsStringAsync()).Should().Contain(mask).And.NotContain(cpf);
    }

    [Fact]
    public async Task Company_ShouldHaveItsCnpjMasked()
    {
        var cnpj = SecurityFixture.UniqueCnpj();

        var response = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Owner.Token,
            new { name = "Empresa", phoneNumber = "+5511999999999", type = "Company", document = cnpj });

        (await response.Content.ReadAsStringAsync())
            .Should().Contain($"**.***.{cnpj[5..8]}/{cnpj[8..12]}-**").And.NotContain(cnpj);
    }

    [Fact]
    public async Task FullDocument_ShouldBeReturned_ToOwnerAndAdmin_AndBeAudited()
    {
        var (id, cpf, _) = await CreateAsync();

        var byOwner = await _f.SendAsync(HttpMethod.Get, $"/clients/{id}/document", _f.Owner.Token);
        var byAdmin = await _f.SendAsync(HttpMethod.Get, $"/clients/{id}/document", _f.Admin.Token);

        (await _f.ReadAsync<DocumentView>(byOwner)).Should().Be(new DocumentView("Individual", cpf));
        (await _f.ReadAsync<DocumentView>(byAdmin)).Should().Be(new DocumentView("Individual", cpf));
        byOwner.Headers.CacheControl!.NoStore.Should().BeTrue();

        var accesses = await _f.ReadDocumentAccessesAsync(id);
        accesses.Select(a => a.OperatorId).Should().BeEquivalentTo([_f.Owner.Id, _f.Admin.Id]);
        accesses.Should().OnlyContain(a => a.AccessedAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task FullDocument_ShouldBeRefused_ToAnOperator_WithoutAnAuditRecord()
    {
        var (id, _, _) = await CreateAsync();

        var response = await _f.SendAsync(HttpMethod.Get, $"/clients/{id}/document", _f.Operator.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.ReadDocumentAccessesAsync(id)).Should().BeEmpty();
    }

    [Fact]
    public async Task FullDocument_OfAnUnknownClient_ShouldBeNotFound_WithoutAnAuditRecord()
    {
        var id = Guid.NewGuid();

        var response = await _f.SendAsync(HttpMethod.Get, $"/clients/{id}/document", _f.Owner.Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _f.ReadDocumentAccessesAsync(id)).Should().BeEmpty();
    }

    [Fact]
    public async Task AuditRecord_ShouldSurviveTheDeletionOfTheClient()
    {
        var (id, _, _) = await CreateAsync();
        await _f.SendAsync(HttpMethod.Get, $"/clients/{id}/document", _f.Owner.Token);

        await _f.SendAsync(HttpMethod.Delete, $"/clients/{id}", _f.Owner.Token);

        (await _f.ReadDocumentAccessesAsync(id)).Should().ContainSingle();
    }

    private sealed record ClientView(Guid Id, string Type, string Document);
    private sealed record DocumentView(string Type, string Document);
}
