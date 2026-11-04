namespace NeoParking.Integration.Tests.Repositories;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Billing.Application;
using NeoParking.Billing.Domain;
using NeoParking.Billing.Domain.Vo;
using NeoParking.Billing.Infrastructure;
using NeoParking.Integration.Tests.Support;

[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class BillingPersistenceTests
{
    private readonly MySqlFixture _mysql;

    public BillingPersistenceTests(MySqlFixture mysql) => _mysql = mysql;

    private async Task<ModuleHost> NewHostAsync() =>
        await ModuleHost.CreateMigratedAsync(await _mysql.CreateDatabasesAsync());

    [Fact]
    public async Task PricingPolicies_ShouldBeFoundByVigency()
    {
        await using var host = await NewHostAsync();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var old = PricingPolicy.Define(Guid.NewGuid(), Money.Create(5m), Money.Create(2m), effectiveFrom: start);
        old.Close(start.AddMonths(1));
        var current = PricingPolicy.Define(Guid.NewGuid(), Money.Create(10m), Money.Create(4m), effectiveFrom: start.AddMonths(1));
        await host.UseAsync<IPricingPolicyRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(old);
            await repository.AddAsync(current);
            await unitOfWork.SaveChangesAsync();
        });

        await host.UseAsync<IPricingPolicyRepository>(async repository =>
        {
            (await repository.GetByIdAsync(old.Id))!.MinimumFee.Should().Be(Money.Create(5m));
            (await repository.GetActiveAsync())!.Id.Should().Be(current.Id);
            (await repository.GetEffectiveAtAsync(start.AddDays(1)))!.Id.Should().Be(old.Id);
            (await repository.GetEffectiveAtAsync(start.AddMonths(2)))!.Id.Should().Be(current.Id);
            (await repository.GetEffectiveAtAsync(start.AddDays(-1))).Should().BeNull();
            (await repository.GetHistoryAsync()).Select(p => p.Id).Should().Equal(old.Id, current.Id);
        });
    }

    [Fact]
    public async Task ClosedTicket_ShouldPersistTheFrozenFee_AndAppearInTheHistory()
    {
        await using var host = await NewHostAsync();
        var entry = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var ticket = Ticket.OpenFromAccess(Guid.NewGuid(), "ABC1D23", entry);
        var policy = PricingPolicy.Define(Guid.NewGuid(), Money.Create(10m), Money.Create(4m), effectiveFrom: entry);
        await host.UseAsync<ITicketRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(ticket);
            await unitOfWork.SaveChangesAsync();
        });
        await host.UseAsync<ITicketRepository, IUnitOfWork>(async (repository, unitOfWork) =>
        {
            var open = await repository.GetByIdAsync(ticket.Id);
            open!.Close(entry.AddHours(3), policy);
            await repository.UpdateAsync(open);
            await unitOfWork.SaveChangesAsync();
        });

        await host.UseAsync<ITicketRepository>(async repository =>
        {
            var closed = await repository.GetByIdAsync(ticket.Id);
            closed!.ChargedFee.Should().NotBeNull();
            closed.ChargedFee!.Amount.Should().Be(Money.Create(18m));
            closed.ChargedFee.PricingPolicyId.Should().Be(policy.Id);
            (await repository.GetFeeHistoryAsync()).Select(t => t.Id).Should().ContainSingle().Which.Should().Be(ticket.Id);
        });
    }

    [Fact]
    public async Task DefiningAPolicy_WhenTheOutboxInsertFails_ShouldKeepThePreviousOneActive()
    {
        await using var host = await NewHostAsync();
        var first = await host.UseAsync<PricingPolicyService, PricingPolicyResponseDTO>(s =>
            s.DefineAsync(Guid.NewGuid(), new DefinePricingPolicyRequest(10m, 4m)));
        await host.UseAsync<BillingDbContext>(c => c.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER force_failure BEFORE INSERT ON billing_outbox_messages FOR EACH ROW "
            + "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'forced failure'"));

        var act = () => host.UseAsync<PricingPolicyService>(s =>
            s.DefineAsync(Guid.NewGuid(), new DefinePricingPolicyRequest(20m, 8m)));

        await act.Should().ThrowAsync<DbUpdateException>();
        var policies = await host.UseAsync<BillingDbContext, List<PricingPolicy>>(c =>
            c.PricingPolicies.AsNoTracking().ToListAsync());
        policies.Should().ContainSingle().Which.Id.Should().Be(first.Id);
        policies[0].EffectiveTo.Should().BeNull();
    }
}
