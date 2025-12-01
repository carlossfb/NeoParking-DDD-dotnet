using FluentAssertions;
using System.Net.Http.Json;
using System.Net;
using main.common.dto;
using Xunit;

namespace Access.Tests.E2E;

// Teste E2E que testa contra a aplicação rodando em localhost
// Para executar: certifique-se que a aplicação está rodando em https://localhost:7071
public class RealClientEndpointTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _baseUrl = "http://localhost:5079"; // URL da aplicação rodando

    public RealClientEndpointTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
    }

    [Fact]
    public async Task POST_CreateClient_WithValidData_ShouldReturnCreated()
    {
        var request = new ClientRequestDTO("João E2E", "+5511999999999", "11144477735", null);

        var response = await _client.PostAsJsonAsync("/clients", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ClientResponseDTO>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("João E2E");
    }

    [Fact]
    public async Task GET_AllClients_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/clients");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<ClientResponseDTO>>();
        result.Should().NotBeNull();
    }



    public void Dispose()
    {
        _client?.Dispose();
    }
}