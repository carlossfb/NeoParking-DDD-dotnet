namespace NeoParking.E2E.Tests.ViewRoutes;

using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-801: a tela de operadores lista contas sem receber o hash da senha.</summary>
[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public sealed class OperatorListTests
{
    private sealed record Item(Guid Id, string Name, string Email, string Role, bool IsActive);
    private sealed record OperatorPage(List<Item> Items, int Page, int PageSize, int TotalCount, int TotalPages);

    private readonly SecurityFixture _f;

    public OperatorListTests(SecurityFixture fixture) => _f = fixture;

    [Fact]
    public async Task List_ShouldReturnOnePageAtATime()
    {
        var first = await ListAsync("?page=1&pageSize=1");
        var second = await ListAsync("?page=2&pageSize=1");

        first.Items.Should().HaveCount(1);
        first.PageSize.Should().Be(1);
        first.TotalCount.Should().BeGreaterThanOrEqualTo(3);
        first.TotalPages.Should().Be(first.TotalCount);
        second.Items.Should().HaveCount(1);
        second.Items[0].Id.Should().NotBe(first.Items[0].Id);
    }

    [Fact]
    public async Task List_ShouldNotExposeThePasswordHash()
    {
        var response = await _f.SendAsync(HttpMethod.Get, "/operators", _f.Admin.Token);

        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain(_f.Owner.Email);
        text.Should().NotContainEquivalentOf("password").And.NotContain("$2");
    }

    [Theory]
    [InlineData("?pageSize=1000", 1, 100)]
    [InlineData("?pageSize=0", 1, 1)]
    [InlineData("?page=0", 1, 20)]
    public async Task List_ShouldKeepPagingInsideTheLimits(string query, int page, int pageSize)
    {
        var result = await ListAsync(query);

        result.Page.Should().Be(page);
        result.PageSize.Should().Be(pageSize);
    }

    [Fact]
    public async Task List_WithAHugePage_ShouldReturnAnEmptyPage()
    {
        var result = await ListAsync($"?page={int.MaxValue}&pageSize=100");

        result.Items.Should().BeEmpty();
    }

    private async Task<OperatorPage> ListAsync(string query)
    {
        var response = await _f.SendAsync(HttpMethod.Get, $"/operators{query}", _f.Owner.Token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await _f.ReadAsync<OperatorPage>(response);
    }
}
