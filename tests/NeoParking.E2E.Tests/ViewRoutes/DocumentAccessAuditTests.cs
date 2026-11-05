namespace NeoParking.E2E.Tests.ViewRoutes;

using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-805: o Owner consulta quem leu o documento, sem ver o documento.</summary>
[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public sealed class DocumentAccessAuditTests
{
    private sealed record Access(Guid OperatorId, DateTime AccessedAt);
    private sealed record AccessPage(List<Access> Items, int Page, int PageSize, int TotalCount);

    private readonly SecurityFixture _f;

    public DocumentAccessAuditTests(SecurityFixture fixture) => _f = fixture;

    [Fact]
    public async Task Audit_ShouldListTheMostRecentReadFirst_WithoutTheDocument()
    {
        var (id, cpf) = await CreateAsync();
        await RevealAsync(id, _f.Admin.Token);
        await RevealAsync(id, _f.Owner.Token);

        var response = await _f.SendAsync(HttpMethod.Get, $"/clients/{id}/document-accesses", _f.Owner.Token);

        (await response.Content.ReadAsStringAsync()).Should().NotContain(cpf).And.NotContainEquivalentOf("document\"");
        var audit = await _f.ReadAsync<AccessPage>(response);
        audit.Items.Select(a => a.OperatorId).Should().Equal(_f.Owner.Id, _f.Admin.Id);
        audit.Items[0].AccessedAt.Should().BeOnOrAfter(audit.Items[1].AccessedAt);
    }

    [Fact]
    public async Task Audit_ShouldBePaged()
    {
        var (id, _) = await CreateAsync();
        await RevealAsync(id, _f.Admin.Token);
        await RevealAsync(id, _f.Owner.Token);

        var audit = await ReadAsync(id, "?page=2&pageSize=1");

        audit.TotalCount.Should().Be(2);
        audit.Items.Should().ContainSingle(a => a.OperatorId == _f.Admin.Id);
    }

    [Fact]
    public async Task Audit_ShouldSurviveTheClientDeletion()
    {
        var (id, _) = await CreateAsync();
        await RevealAsync(id, _f.Admin.Token);
        (await _f.SendAsync(HttpMethod.Delete, $"/clients/{id}", _f.Owner.Token))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadAsync(id, "")).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Audit_OfAClientWithoutReads_ShouldBeEmpty()
    {
        (await ReadAsync(Guid.NewGuid(), "")).Items.Should().BeEmpty();
    }

    private async Task<AccessPage> ReadAsync(Guid id, string query) =>
        await _f.ReadAsync<AccessPage>(await _f.SendAsync(HttpMethod.Get, $"/clients/{id}/document-accesses{query}", _f.Owner.Token));

    private async Task RevealAsync(Guid id, string token) =>
        (await _f.SendAsync(HttpMethod.Get, $"/clients/{id}/document", token)).EnsureSuccessStatusCode();

    private async Task<(Guid Id, string Cpf)> CreateAsync()
    {
        var cpf = SecurityFixture.UniqueCpf();
        var response = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Owner.Token,
            new { name = "Cliente", phoneNumber = "+5511999999999", type = "Individual", document = cpf });
        return ((await _f.ReadAsync<SecurityFixture.IdResponse>(response)).Id, cpf);
    }
}
