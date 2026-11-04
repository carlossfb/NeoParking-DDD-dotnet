namespace NeoParking.E2E.Tests.Privacy;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-601: placa e documento não aparecem em logs reais, tracing nem respostas de erro.</summary>
[Collection(SecurityCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SensitiveDataLeakTests
{
    private readonly SecurityFixture _f;

    public SensitiveDataLeakTests(SecurityFixture fixture) => _f = fixture;

    [Fact]
    public async Task InvalidPlate_ShouldNotLeakTheValue()
    {
        var invalid = "LEAK" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var response = await _f.SendAsync(HttpMethod.Post, "/access/tickets", _f.Operator.Token, new { plate = invalid });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(invalid);
        _f.Logs.Text.Should().Contain("Invalid plate format").And.NotContain(invalid);
    }

    [Fact]
    public async Task SecondOpenTicket_ShouldNotLeakThePlate()
    {
        var plate = SecurityFixture.UniquePlate();
        await _f.SendAsync(HttpMethod.Post, "/access/tickets", _f.Operator.Token, new { plate });

        var response = await _f.SendAsync(HttpMethod.Post, "/access/tickets", _f.Operator.Token, new { plate });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(plate);
        _f.Logs.Text.Should().NotContain(plate);
    }

    [Fact]
    public async Task PlateInUseByAnotherClient_ShouldNotLeakItFromTheDuplicateKeyError()
    {
        var plate = SecurityFixture.UniquePlate();
        var first = await CreateClientAsync();
        var second = await CreateClientAsync();
        await _f.SendAsync(HttpMethod.Post, $"/clients/{first}/vehicles", _f.Operator.Token, new { plate });

        var response = await _f.SendAsync(HttpMethod.Post, $"/clients/{second}/vehicles", _f.Operator.Token, new { plate });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(plate);
        _f.Logs.Text.Should().NotContain(plate);
    }

    [Fact]
    public async Task DocumentInUse_ShouldNotLeakItFromTheDuplicateKeyError()
    {
        var cpf = SecurityFixture.UniqueCpf();
        var body = new { name = "Cliente", phoneNumber = "+5511999999999", cpf };
        await _f.SendAsync(HttpMethod.Post, "/clients", _f.Operator.Token, body);

        var response = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Operator.Token, body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(cpf);
        _f.Logs.Text.Should().NotContain(cpf);
    }

    [Fact]
    public async Task ExitByPlate_ShouldLogAndTraceTheRoutePattern_NotThePlate()
    {
        var plate = SecurityFixture.UniquePlate();
        var traced = new ConcurrentQueue<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => traced.Enqueue(
                string.Join(' ', activity.DisplayName, string.Join(' ', activity.TagObjects.Select(t => $"{t.Key}={t.Value}")))),
        };
        ActivitySource.AddActivityListener(listener);
        await _f.SendAsync(HttpMethod.Post, "/access/tickets", _f.Operator.Token, new { plate });

        var response = await _f.SendAsync(
            HttpMethod.Post, $"/access/tickets/by-plate/{plate}/exit", _f.Operator.Token, new { reason = "Lost ticket" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _f.Logs.Text.Should().Contain("/access/tickets/by-plate/{plate}/exit").And.NotContain(plate);
        traced.Should().Contain(entry => entry.Contains("url.path=/access/tickets/by-plate/{plate}/exit"));
        traced.Should().NotContain(entry => entry.Contains(plate));
    }

    private async Task<Guid> CreateClientAsync()
    {
        var response = await _f.SendAsync(HttpMethod.Post, "/clients", _f.Operator.Token,
            new { name = "Cliente", phoneNumber = "+5511999999999", cpf = SecurityFixture.UniqueCpf() });
        return (await _f.ReadAsync<SecurityFixture.IdResponse>(response)).Id;
    }
}
