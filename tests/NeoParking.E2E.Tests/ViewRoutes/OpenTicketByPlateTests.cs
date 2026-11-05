namespace NeoParking.E2E.Tests.ViewRoutes;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using FluentAssertions;
using NeoParking.E2E.Tests.Security;

/// <summary>TD-803: conferir o ticket aberto antes da saída, com a placa fora da URL.</summary>
[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public sealed class OpenTicketByPlateTests
{
    private const string Route = "/access/tickets/open-by-plate";

    private sealed record TicketResponse(Guid Id, string Code, string Plate, string Status);

    private readonly SecurityFixture _f;

    public OpenTicketByPlateTests(SecurityFixture fixture) => _f = fixture;

    [Fact]
    public async Task OpenTicket_ShouldBeFound_WithoutClosingIt()
    {
        var plate = SecurityFixture.UniquePlate();
        var opened = await OpenAsync(plate);

        var response = await _f.SendAsync(HttpMethod.Post, Route, _f.Operator.Token, new { plate = plate.ToLowerInvariant() });

        var ticket = await _f.ReadAsync<TicketResponse>(response);
        ticket.Id.Should().Be(opened.Id);
        ticket.Status.Should().Be("Open");
    }

    [Fact]
    public async Task ClosedTicket_ShouldNotBeFound()
    {
        var plate = SecurityFixture.UniquePlate();
        var opened = await OpenAsync(plate);
        await _f.SendAsync(HttpMethod.Post, $"/access/tickets/{opened.Id}/exit", _f.Operator.Token, new { });

        var response = await _f.SendAsync(HttpMethod.Post, Route, _f.Operator.Token, new { plate });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(plate);
    }

    [Fact]
    public async Task InvalidPlate_ShouldBeRejected_WithoutLeakingIt()
    {
        var invalid = "LEAK" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var response = await _f.SendAsync(HttpMethod.Post, Route, _f.Operator.Token, new { plate = invalid });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(invalid);
        _f.Logs.Text.Should().NotContain(invalid);
    }

    [Fact]
    public async Task Plate_ShouldStayOutOfLogsAndTraces()
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
        await OpenAsync(plate);

        var response = await _f.SendAsync(HttpMethod.Post, Route, _f.Operator.Token, new { plate });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        traced.Should().Contain(entry => entry.Contains($"url.path={Route}"));
        traced.Should().NotContain(entry => entry.Contains(plate));
        _f.Logs.Text.Should().NotContain(plate);
    }

    private async Task<TicketResponse> OpenAsync(string plate)
    {
        var response = await _f.SendAsync(HttpMethod.Post, "/access/tickets", _f.Operator.Token, new { plate });
        return await _f.ReadAsync<TicketResponse>(response);
    }
}
