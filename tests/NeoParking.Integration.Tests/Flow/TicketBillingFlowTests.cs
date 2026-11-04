namespace NeoParking.Integration.Tests.Flow;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Application;
using NeoParking.Billing.Application;
using NeoParking.Billing.Infrastructure;
using NeoParking.Integration.Tests.Support;
using NeoParking.Shared.Kernel.Outbox;
using AccessContext = NeoParking.Access.Infrastructure.AccessDbContext;
using BillingTicket = NeoParking.Billing.Application.TicketResponseDTO;
using BillingTicketService = NeoParking.Billing.Application.TicketService;

/// <summary>Da publicação no Access até a Fee no Billing, com os handlers e o outbox reais.</summary>
[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TicketBillingFlowTests
{
    private static readonly OutboxRetryPolicy NoDelay = new(3, TimeSpan.Zero, TimeSpan.Zero);

    private readonly MySqlFixture _mysql;

    public TicketBillingFlowTests(MySqlFixture mysql) => _mysql = mysql;

    [Fact]
    public async Task EntryAndExit_ShouldReachBilling_AndFreezeTheFee()
    {
        await using var host = await ModuleHost.CreateMigratedAsync(await _mysql.CreateDatabasesAsync());
        await DefinePolicyAsync(host);

        var ticket = await OpenAndCloseTicketAsync(host);
        await host.AccessOutbox(NoDelay).ProcessOnceAsync();

        var billed = await GetBilledAsync(host, ticket);
        billed.Status.Should().Be("Closed");
        billed.Fee.Should().NotBeNull();
        billed.Fee!.Amount.Should().Be(10m);
    }

    [Fact]
    public async Task RedeliveredMessages_ShouldNotDuplicateTheEffect()
    {
        await using var host = await ModuleHost.CreateMigratedAsync(await _mysql.CreateDatabasesAsync());
        await DefinePolicyAsync(host);
        var ticket = await OpenAndCloseTicketAsync(host);
        var outbox = host.AccessOutbox(NoDelay);
        await outbox.ProcessOnceAsync();
        var first = await GetBilledAsync(host, ticket);

        await host.UseAsync<AccessContext>(c => c.Database.ExecuteSqlRawAsync(
            "UPDATE outbox_messages SET PublishedAt = NULL"));
        await outbox.ProcessOnceAsync();

        var second = await GetBilledAsync(host, ticket);
        second.Fee.Should().BeEquivalentTo(first.Fee);
        (await host.UseAsync<BillingDbContext, int>(c => c.Tickets.CountAsync())).Should().Be(1);
        (await host.UseAsync<AccessContext, int>(c => c.OutboxMessages.CountAsync(m => m.PublishedAt == null)))
            .Should().Be(0);
    }

    [Fact]
    public async Task TicketClosedBeforeTheFirstPolicy_ShouldSurviveExhaustedAttempts_AndBeCharged()
    {
        await using var host = await ModuleHost.CreateMigratedAsync(await _mysql.CreateDatabasesAsync());
        var ticket = await OpenAndCloseTicketAsync(host);
        var outbox = host.AccessOutbox(NoDelay);

        // Mais ciclos do que o limite configurado de tentativas, ainda sem política.
        for (var cycle = 0; cycle < NoDelay.MaxAttempts * 2; cycle++)
            await outbox.ProcessOnceAsync();

        var closedEvent = await host.UseAsync<AccessContext, OutboxMessage>(c =>
            c.OutboxMessages.AsNoTracking().SingleAsync(m => m.PublishedAt == null));
        closedEvent.AttemptCount.Should().BeGreaterThan(NoDelay.MaxAttempts);
        closedEvent.DeadLetteredAt.Should().BeNull("the charge would be lost");
        (await GetBilledAsync(host, ticket)).Fee.Should().BeNull();

        await DefinePolicyAsync(host);
        await outbox.ProcessOnceAsync();

        var billed = await GetBilledAsync(host, ticket);
        billed.Status.Should().Be("Closed");
        billed.Fee!.Amount.Should().Be(10m);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("plate")]
    public async Task ExitByAnyMethod_ShouldFreezeASingleFee(string method)
    {
        await using var host = await ModuleHost.CreateMigratedAsync(await _mysql.CreateDatabasesAsync());
        await DefinePolicyAsync(host);
        var opened = await host.UseAsync<ITicketService, NeoParking.Access.Application.TicketResponseDTO>(s =>
            s.RegisterEntryAsync(new RegisterEntryRequest("ABC1D23")));

        await host.UseAsync<ITicketService>(s => method == "code"
            ? s.RegisterExitByCodeAsync(opened.Code, new RegisterExitRequest())
            : s.RegisterExitByPlateAsync("ABC1D23", new RegisterExitByPlateRequest("Ticket perdido"), Guid.NewGuid()));
        await host.AccessOutbox(NoDelay).ProcessOnceAsync();

        var billed = await GetBilledAsync(host, opened.Id);
        billed.Status.Should().Be("Closed");
        billed.Fee!.Amount.Should().Be(10m);
        (await host.UseAsync<AccessContext, int>(c => c.OutboxMessages.CountAsync())).Should().Be(2, "one opened, one closed");
    }

    private static Task DefinePolicyAsync(ModuleHost host) =>
        host.UseAsync<PricingPolicyService>(s => s.DefineAsync(
            Guid.NewGuid(), new DefinePricingPolicyRequest(10m, 4m)));

    private static async Task<Guid> OpenAndCloseTicketAsync(ModuleHost host)
    {
        var opened = await host.UseAsync<ITicketService, NeoParking.Access.Application.TicketResponseDTO>(s =>
            s.RegisterEntryAsync(new RegisterEntryRequest("ABC1D23")));
        await host.UseAsync<ITicketService>(s =>
            s.RegisterExitAsync(opened.Id, new RegisterExitRequest()));
        return opened.Id;
    }

    private static Task<BillingTicket> GetBilledAsync(ModuleHost host, Guid id) =>
        host.UseAsync<BillingTicketService, BillingTicket>(s => s.GetByIdAsync(id));
}
