namespace NeoParking.E2E.Tests;

using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NeoParking.Shared.Kernel.Persistence;

/// <summary>
/// Sem coleção: este fixture é só desta classe, porque o teste derruba o banco.
/// Um único teste, para a ordem entre banco no ar e fora do ar ser garantida.
/// </summary>
[Trait("Category", "Integration")]
public class HealthEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public HealthEndpointTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Health_ShouldBeAnonymous_AndFollowTheDatabase_WithoutExposingDetails()
    {
        var client = _fixture.CreateClient();

        using (var scope = _fixture.Services.CreateScope())
            scope.ServiceProvider.GetServices<IDatabaseProbe>().Should().HaveCount(3, "cada módulo tem o seu banco");

        var up = await client.GetAsync("/health");
        up.StatusCode.Should().Be(HttpStatusCode.OK);
        (await up.Content.ReadAsStringAsync()).Should().Be("Healthy");

        (await client.PostAsync("/health", null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

        await _fixture.StopDatabaseAsync();

        var down = await client.GetAsync("/health");
        down.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await down.Content.ReadAsStringAsync()).Should().Be("Unhealthy");
    }
}
