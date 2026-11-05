namespace NeoParking.E2E.Tests.ViewRoutes;

using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-802: a lista é paginada, e a busca leva placa e documento no corpo, nunca na URL.</summary>
[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public sealed class ClientListAndSearchTests
{
    private sealed record Item(Guid Id, string Document);
    private sealed record ClientPage(List<Item> Items, int Page, int PageSize, int TotalCount);

    private readonly SecurityFixture _f;

    public ClientListAndSearchTests(SecurityFixture fixture) => _f = fixture;

    [Fact]
    public async Task List_ShouldReturnOnePageAtATime_InAStableOrder()
    {
        await CreateAsync();
        await CreateAsync();

        var first = await ListAsync("?page=1&pageSize=1");
        var second = await ListAsync("?page=2&pageSize=1");
        var again = await ListAsync("?page=1&pageSize=1");

        first.Items.Should().HaveCount(1);
        first.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        second.Items[0].Id.Should().NotBe(first.Items[0].Id);
        again.Items[0].Id.Should().Be(first.Items[0].Id);
    }

    [Fact]
    public async Task List_ShouldLimitThePageSize()
    {
        (await ListAsync("?pageSize=1000")).PageSize.Should().Be(100);
    }

    [Fact]
    public async Task SearchByPlate_ShouldFindTheClient_AndNormalizeThePlate()
    {
        var (id, _, plate) = await CreateAsync();

        var response = await SearchAsync(new { plate = $"{plate[..3].ToLowerInvariant()}-{plate[3..]}" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _f.ReadAsync<Item>(response)).Id.Should().Be(id);
    }

    [Fact]
    public async Task SearchByDocument_ShouldFindTheClient_AndKeepTheDocumentMasked()
    {
        var (id, cpf, _) = await CreateAsync();

        var response = await SearchAsync(new { documentType = "Individual", document = cpf });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(cpf);
        var found = await _f.ReadAsync<Item>(response);
        found.Id.Should().Be(id);
        found.Document.Should().Contain("*");
    }

    [Fact]
    public async Task Search_WithoutAMatch_ShouldReturnNotFound()
    {
        (await SearchAsync(new { plate = SecurityFixture.UniquePlate() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SearchAsync(new { documentType = "Individual", document = SecurityFixture.UniqueCpf() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_ShouldRequireExactlyOneCriterion()
    {
        (await SearchAsync(new { })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await SearchAsync(new { plate = SecurityFixture.UniquePlate(), documentType = "Individual", document = SecurityFixture.UniqueCpf() }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await SearchAsync(new { document = SecurityFixture.UniqueCpf() }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Search_ShouldNotLeakTheValues()
    {
        var (_, cpf, plate) = await CreateAsync();
        var invalidPlate = "LEAK" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var invalidCpf = "9" + Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);

        await SearchAsync(new { plate });
        await SearchAsync(new { documentType = "Individual", document = cpf });
        var badPlate = await SearchAsync(new { plate = invalidPlate });
        var badCpf = await SearchAsync(new { documentType = "Individual", document = invalidCpf });

        (await badPlate.Content.ReadAsStringAsync()).Should().NotContain(invalidPlate);
        (await badCpf.Content.ReadAsStringAsync()).Should().NotContain(invalidCpf);
        _f.Logs.Text.Should().NotContain(plate).And.NotContain(cpf)
            .And.NotContain(invalidPlate).And.NotContain(invalidCpf);
    }

    private Task<HttpResponseMessage> SearchAsync(object body) =>
        _f.SendAsync(HttpMethod.Post, "/clients/search", _f.Operator.Token, body);

    private async Task<ClientPage> ListAsync(string query)
    {
        var response = await _f.SendAsync(HttpMethod.Get, $"/clients{query}", _f.Operator.Token);
        return await _f.ReadAsync<ClientPage>(response);
    }

    private async Task<(Guid Id, string Cpf, string Plate)> CreateAsync()
    {
        var cpf = SecurityFixture.UniqueCpf();
        var plate = SecurityFixture.UniquePlate();
        var response = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Operator.Token, new
        {
            name = "Cliente",
            phoneNumber = "+5511999999999",
            type = "Individual",
            document = cpf,
            vehicles = new[] { new { plate } }
        });
        return ((await _f.ReadAsync<SecurityFixture.IdResponse>(response)).Id, cpf, plate);
    }
}
