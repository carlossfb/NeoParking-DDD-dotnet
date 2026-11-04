namespace NeoParking.E2E.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

// Usa Testcontainers: fica no grupo Integration, fora do filtro Category!=Integration.
[Trait("Category", "Integration")]
public class ApiSmokeTests : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(ApiFixture fixture) => _client = fixture.CreateClient();

    [Fact]
    public async Task OwnerBootstrap_Login_AndAuthenticatedCall_ShouldWorkOverHttp()
    {
        const string email = "owner@neoparking.test";
        const string password = "E2e-Password-123!";

        var bootstrap = await _client.PostAsJsonAsync("/operators/owner",
            new { name = "Owner", email, password });
        bootstrap.StatusCode.Should().Be(HttpStatusCode.Created);
        var owner = await bootstrap.Content.ReadFromJsonAsync<OperatorResponse>();

        var login = await _client.PostAsJsonAsync("/operators/login", new { email, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Token;

        var anonymous = await _client.GetAsync($"/operators/{owner!.Id}");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/operators/{owner.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var authenticated = await _client.SendAsync(request);

        authenticated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await authenticated.Content.ReadFromJsonAsync<OperatorResponse>())!.Email.Should().Be(email);
    }

    private sealed record OperatorResponse(Guid Id, string Email);
    private sealed record LoginResponse(string Token);
}
