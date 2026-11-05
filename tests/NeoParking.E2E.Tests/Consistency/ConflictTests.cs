namespace NeoParking.E2E.Tests.Consistency;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>A perda de uma corrida vira 409 no HTTP, sem detalhes do banco na resposta.</summary>
[Collection(SecurityCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ConflictTests
{
    private readonly SecurityFixture _f;

    public ConflictTests(SecurityFixture fixture) => _f = fixture;

    [Fact]
    public async Task ConcurrentOpenTickets_ShouldReturnOneSuccessAndOneConflict()
    {
        var plate = SecurityFixture.UniquePlate();

        await ShouldHaveOneWinnerAsync(HttpStatusCode.Created, () => _f.SendAsync(
            HttpMethod.Post, "/access/tickets", _f.Owner.Token, new { plate }));
    }

    [Fact]
    public async Task ConcurrentClients_WithTheSameCpf_ShouldReturnOneSuccessAndOneConflict()
    {
        var cpf = SecurityFixture.UniqueCpf();

        await ShouldHaveOneWinnerAsync(HttpStatusCode.Created, () => _f.SendAsync(
            HttpMethod.Post, "/clients", _f.Owner.Token,
            new { name = "Cliente", phoneNumber = "+5511999999999", type = "Individual", document = cpf }));
    }

    [Fact]
    public async Task ConcurrentVehicles_WithTheSamePlate_ShouldReturnOneSuccessAndOneConflict()
    {
        var first = await CreateClientAsync();
        var second = await CreateClientAsync();
        var plate = SecurityFixture.UniquePlate();
        var clients = new Queue<Guid>([first, second]);

        await ShouldHaveOneWinnerAsync(HttpStatusCode.Created, () => _f.SendAsync(
            HttpMethod.Post, $"/clients/{clients.Dequeue()}/vehicles", _f.Owner.Token, new { plate }));
    }

    [Fact]
    public async Task ConcurrentPricingPolicies_ShouldNeverFailWithAnythingButAConflict()
    {
        Func<Task<HttpResponseMessage>> define = () => _f.SendAsync(
            HttpMethod.Post, "/billing/pricing-policies", _f.Owner.Token, new
            {
                minimumFee = 10m,
                hourlyRate = 4m,
                minimumFeeCoverageMinutes = 60,
                gracePeriodMinutes = 0
            });

        var responses = await Task.WhenAll(Task.Run(define), Task.Run(define));

        // Se a segunda começar depois do commit da primeira, ela é uma revisão legítima.
        responses.Select(r => r.StatusCode).Should().Contain(HttpStatusCode.Created);
        responses.Select(r => r.StatusCode).Should()
            .OnlyContain(s => s == HttpStatusCode.Created || s == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OpeningASecondTicket_ShouldAlsoBeAConflict()
    {
        var plate = SecurityFixture.UniquePlate();
        (await _f.SendAsync(HttpMethod.Post, "/access/tickets", _f.Owner.Token, new { plate }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _f.SendAsync(HttpMethod.Post, "/access/tickets", _f.Owner.Token, new { plate });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<Guid> CreateClientAsync()
    {
        var response = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Owner.Token,
            new { name = "Cliente", phoneNumber = "+5511999999999", type = "Individual", document = SecurityFixture.UniqueCpf() });
        return (await _f.ReadAsync<SecurityFixture.IdResponse>(response)).Id;
    }

    internal static async Task ShouldHaveOneWinnerAsync(HttpStatusCode success, Func<Task<HttpResponseMessage>> call)
    {
        var responses = await Task.WhenAll(Task.Run(call), Task.Run(call));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([success, HttpStatusCode.Conflict]);

        var conflict = await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict)
            .Content.ReadAsStringAsync();
        conflict.Should().NotContainAny("Duplicate", "ux_", "ix_", "MySql", "SQL");
    }
}

/// <summary>O bootstrap do Owner precisa de um banco vazio, então usa uma API só dele.</summary>
[Trait("Category", "Integration")]
public sealed class OwnerBootstrapRaceTests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;

    public OwnerBootstrapRaceTests(ApiFixture fixture) => _client = fixture.CreateClient();

    [Fact]
    public async Task ConcurrentBootstraps_ShouldReturnOneSuccessAndOneConflict()
    {
        await ConflictTests.ShouldHaveOneWinnerAsync(HttpStatusCode.Created, () => _client.PostAsJsonAsync(
            "/operators/owner",
            new { name = "Owner", email = SecurityFixture.UniqueEmail(), password = SecurityFixture.Password }));
    }
}
