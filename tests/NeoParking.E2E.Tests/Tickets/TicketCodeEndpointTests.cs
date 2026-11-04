namespace NeoParking.E2E.Tests.Tickets;

using System.Net;
using System.Text;
using FluentAssertions;
using NeoParking.Access.Domain;
using NeoParking.E2E.Tests.Security;

/// <summary>C2 pela API real: código, saída por código e saída por placa.</summary>
[Trait("Category", "Integration")]
[Collection(SecurityCollection.Name)]
public class TicketCodeEndpointTests
{
    private sealed record TicketBody(Guid Id, string Code, string Plate, string Status, string? ExitMethod);

    private readonly SecurityFixture _f;

    public TicketCodeEndpointTests(SecurityFixture fixture) => _f = fixture;

    private async Task<TicketBody> OpenAsync() => await _f.ReadAsync<TicketBody>(await _f.SendAsync(
        HttpMethod.Post, "/access/tickets", _f.Operator.Token, new { plate = SecurityFixture.UniquePlate() }));

    private Task<HttpResponseMessage> PostAsync(string path, object body) =>
        _f.SendAsync(HttpMethod.Post, path, _f.Operator.Token, body);

    [Fact]
    public async Task Entry_ShouldReturnACode_ThatFindsTheTicket()
    {
        var opened = await OpenAsync();

        opened.Code.Should().MatchRegex("^[0-9A-F]{20}$");
        var found = await _f.ReadAsync<TicketBody>(await _f.SendAsync(
            HttpMethod.Get, $"/access/tickets/by-code/{opened.Code}", _f.Operator.Token));
        found.Id.Should().Be(opened.Id);
    }

    [Theory]
    [InlineData("0123456789ABCDEF0123")]
    [InlineData("not-a-code")]
    public async Task UnknownCode_ShouldReturn404(string code)
    {
        (await _f.SendAsync(HttpMethod.Get, $"/access/tickets/by-code/{code}", _f.Operator.Token))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PostAsync($"/access/tickets/by-code/{code}/exit", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExitByCode_ShouldClose_AndASecondExitShouldReturn422()
    {
        var opened = await OpenAsync();

        var closed = await _f.ReadAsync<TicketBody>(await PostAsync($"/access/tickets/by-code/{opened.Code}/exit", new { }));
        closed.Status.Should().Be("Closed");
        closed.ExitMethod.Should().Be("Code");

        (await PostAsync($"/access/tickets/by-code/{opened.Code}/exit", new { }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"reason\":\"   \"}")]
    public async Task ExitByPlate_WithoutReason_ShouldReturn422_AndKeepTheTicketOpen(string json)
    {
        var opened = await OpenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/access/tickets/by-plate/{opened.Plate}/exit");
        request.Headers.Authorization = new("Bearer", _f.Operator.Token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _f.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await _f.ReadAccessTicketAsync(opened.Id)).Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task ExitByPlate_ShouldRecordTheReason_AndTheAuthorFromTheToken()
    {
        var opened = await OpenAsync();

        // Um operatorId no corpo não pode substituir o autor do token.
        var closed = await _f.ReadAsync<TicketBody>(await PostAsync(
            $"/access/tickets/by-plate/{opened.Plate}/exit",
            new { reason = "Ticket perdido", operatorId = _f.Owner.Id }));

        closed.Status.Should().Be("Closed");
        closed.ExitMethod.Should().Be("Plate");
        var stored = await _f.ReadAccessTicketAsync(opened.Id);
        stored.ExitReason.Should().Be("Ticket perdido");
        stored.ExitOperatorId.Should().Be(_f.Operator.Id);
    }

    [Fact]
    public async Task ExitByPlate_WithoutAnOpenTicket_ShouldReturn404()
    {
        (await PostAsync($"/access/tickets/by-plate/{SecurityFixture.UniquePlate()}/exit", new { reason = "Ticket perdido" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
