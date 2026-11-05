namespace NeoParking.E2E.Tests.Security;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeoParking.Access.Domain;
using NeoParking.Access.Infrastructure;
using NeoParking.Management.Application;

[CollectionDefinition(Name)]
public sealed class SecurityCollection : ICollectionFixture<SecurityFixture>
{
    public const string Name = "Security";
}

/// <summary>
/// API real com um operador de cada papel e os dados que os testes de autorização consultam.
/// Contas são semeadas pelo serviço, para que o preparo não dependa das rotas sob teste.
/// </summary>
public sealed class SecurityFixture : IAsyncLifetime
{
    public const string Password = "E2e-Password-123!";

    private readonly ApiFixture _api = new();

    public HttpClient Client { get; private set; } = null!;
    public LogCapture Logs => _api.Logs;
    public Actor Owner { get; private set; } = null!;
    public Actor Admin { get; private set; } = null!;
    public Actor Operator { get; private set; } = null!;
    public Guid BillingTicketId { get; private set; }

    public async Task InitializeAsync()
    {
        await _api.InitializeAsync();
        Client = _api.CreateClient();

        var email = UniqueEmail();
        var bootstrap = await Client.PostAsJsonAsync("/operators/owner",
            new { name = "Owner", email, password = Password });
        bootstrap.EnsureSuccessStatusCode();
        var owner = await bootstrap.Content.ReadFromJsonAsync<IdResponse>();
        Owner = new Actor(owner!.Id, email, await LoginAsync(email));

        Admin = await CreateActorAsync("Admin");
        Operator = await CreateActorAsync("Operator");

        await SeedPricingPolicyAsync();
        BillingTicketId = await SeedTicketKnownToBillingAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _api.DisposeAsync();
    }

    public async Task<Actor> CreateActorAsync(string role)
    {
        var email = UniqueEmail();
        var created = await WithServiceAsync(s => s.CreateOperatorAsync(
            Owner.Id, new CreateOperatorRequest("E2E", email, Password, role)));
        return new Actor(created.Id, email, await LoginAsync(email));
    }

    public Task DeactivateAsync(Guid id) => WithServiceAsync(async s =>
    {
        await s.DeactivateAsync(Owner.Id, id);
        return true;
    });

    public Task DemoteAsync(Guid id) => WithServiceAsync(async s =>
    {
        await s.DemoteToOperatorAsync(Owner.Id, id);
        return true;
    });

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string? token = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await Client.SendAsync(request);
    }

    public async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@neoparking.test";

    public static string UniquePlate()
    {
        var letters = new string(Enumerable.Range(0, 3)
            .Select(_ => (char)('A' + Random.Shared.Next(26))).ToArray());
        return $"{letters}{Random.Shared.Next(1000, 10000)}";
    }

    public static string UniqueCpf()
    {
        var digits = Enumerable.Range(0, 9).Select(_ => Random.Shared.Next(10)).ToList();
        digits[0] = 1;
        digits[1] = 2;
        digits.Add(CheckDigit(digits));
        digits.Add(CheckDigit(digits));
        return string.Concat(digits);
    }

    /// <summary>CNPJ alfanumérico válido: valor do caractere = ASCII − 48, pesos oficiais da Receita Federal.</summary>
    public static string UniqueCnpj()
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var body = new string(Enumerable.Range(0, 12).Select(_ => alphabet[Random.Shared.Next(alphabet.Length)]).ToArray());
        int[] firstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] secondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var first = CnpjDigit(body, firstWeights);
        var second = CnpjDigit(body + first, secondWeights);
        return $"{body}{first}{second}";
    }

    private static int CnpjDigit(string value, int[] weights)
    {
        var rest = weights.Select((weight, i) => (value[i] - 48) * weight).Sum() % 11;
        return rest < 2 ? 0 : 11 - rest;
    }

    private static int CheckDigit(IReadOnlyList<int> digits)
    {
        var weight = digits.Count + 1;
        var sum = digits.Select((d, i) => d * (weight - i)).Sum();
        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }

    private async Task<string> LoginAsync(string email)
    {
        var login = await Client.PostAsJsonAsync("/operators/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
    }

    /// <summary>Lê o ticket direto do banco, para conferir dados que a API não devolve.</summary>
    public async Task<Ticket> ReadAccessTicketAsync(Guid id)
    {
        using var scope = _api.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        return await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == id);
    }

    /// <summary>A linha crua do cliente, com todas as colunas, como quem consulta o banco a veria.</summary>
    public async Task<string> ReadClientRowAsync(Guid clientId)
    {
        using var scope = _api.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT * FROM clients WHERE Id = '{clientId}'";
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return string.Join('|', Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetValue(i)));
    }

    /// <summary>Lê a auditoria direto do banco, sem depender da rota que a expõe.</summary>
    public async Task<List<ClientDocumentAccess>> ReadDocumentAccessesAsync(Guid clientId)
    {
        using var scope = _api.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        return await context.ClientDocumentAccesses.AsNoTracking().Where(a => a.ClientId == clientId).ToListAsync();
    }

    private async Task<T> WithServiceAsync<T>(Func<OperatorService, Task<T>> action)
    {
        using var scope = _api.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<OperatorService>());
    }

    private async Task SeedPricingPolicyAsync()
    {
        var response = await SendAsync(HttpMethod.Post, "/billing/pricing-policies", Owner.Token, new
        {
            ownerId = Owner.Id,
            minimumFee = 10m,
            hourlyRate = 4m,
            minimumFeeCoverageMinutes = 60,
            gracePeriodMinutes = 0
        });
        response.EnsureSuccessStatusCode();
    }

    // O Billing só conhece o ticket depois que o outbox do Access entrega o evento.
    private async Task<Guid> SeedTicketKnownToBillingAsync()
    {
        var opened = await SendAsync(HttpMethod.Post, "/access/tickets", Owner.Token, new { plate = UniquePlate() });
        var ticket = await ReadAsync<IdResponse>(opened);

        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            var response = await SendAsync(HttpMethod.Get, $"/billing/tickets/{ticket.Id}", Owner.Token);
            if (response.StatusCode == HttpStatusCode.OK)
                return ticket.Id;
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException("The ticket did not reach Billing through the outbox.");
    }

    public sealed record Actor(Guid Id, string Email, string Token);
    public sealed record IdResponse(Guid Id);
    private sealed record TokenResponse(string Token);
}
