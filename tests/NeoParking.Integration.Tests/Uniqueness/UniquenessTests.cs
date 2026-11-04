namespace NeoParking.Integration.Tests.Uniqueness;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Application;
using NeoParking.Access.Infrastructure;
using NeoParking.Billing.Application;
using TicketResponseDTO = NeoParking.Access.Application.TicketResponseDTO;
using NeoParking.Billing.Infrastructure;
using NeoParking.Integration.Tests.Support;
using NeoParking.Management.Application;
using NeoParking.Management.Infrastructure;
using NeoParking.Shared.Kernel.Exceptions;
using AccessTicket = NeoParking.Access.Domain.Ticket;
using Plate = NeoParking.Access.Domain.Plate;
using Money = NeoParking.Billing.Domain.Vo.Money;
using OperatorRole = NeoParking.Management.Domain.OperatorRole;
using OperatorUser = NeoParking.Management.Domain.OperatorUser;
using PasswordHash = NeoParking.Management.Domain.Vo.PasswordHash;
using PricingPolicy = NeoParking.Billing.Domain.PricingPolicy;
using AccessUnitOfWork = NeoParking.Access.Application.IUnitOfWork;
using BillingUnitOfWork = NeoParking.Billing.Application.IUnitOfWork;
using ManagementUnitOfWork = NeoParking.Management.Application.IUnitOfWork;

/// <summary>
/// Duas operações concorrentes para cada invariante: só uma é gravada. Os testes "Bypass" pulam a
/// verificação prévia dos serviços, provando que quem garante a regra é o índice no banco.
/// </summary>
[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class UniquenessTests
{
    private const string Hash = "$2a$11$abcdefghijklmnopqrstuuABCDEFGHIJKLMNOPQRSTUVWXYZ01234";
    private const string ValidCpf = "11144477735";
    private const string OtherCpf = "98765432100";
    private const string PhoneNumberValue = "+5511999999999";

    private readonly MySqlFixture _mysql;

    public UniquenessTests(MySqlFixture mysql) => _mysql = mysql;

    private async Task<ModuleHost> NewHostAsync() =>
        await ModuleHost.CreateMigratedAsync(await _mysql.CreateDatabasesAsync());

    [Fact]
    public async Task ConcurrentOwners_ShouldKeepOnlyOne()
    {
        await using var host = await NewHostAsync();

        await ShouldHaveOneWinnerAsync(
            () => CreateOwnerAsync(host, "a@neoparking.test"),
            () => CreateOwnerAsync(host, "b@neoparking.test"));

        (await host.UseAsync<ManagementDbContext, int>(c => c.Operators.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentOpenTickets_ForTheSamePlate_ShouldKeepOnlyOne()
    {
        await using var host = await NewHostAsync();

        await ShouldHaveOneWinnerAsync(() => OpenTicketAsync(host), () => OpenTicketAsync(host));

        (await host.UseAsync<AccessDbContext, int>(c => c.Tickets.CountAsync())).Should().Be(1);
        (await host.UseAsync<AccessDbContext, int>(c => c.OutboxMessages.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentPolicies_ShouldKeepOnlyOneActive()
    {
        await using var host = await NewHostAsync();

        await ShouldKeepOneActivePolicyAsync(host);
    }

    [Fact]
    public async Task ConcurrentRevisions_OfAnActivePolicy_ShouldKeepOnlyOneActive()
    {
        await using var host = await NewHostAsync();
        await DefinePolicyAsync(host);

        await ShouldKeepOneActivePolicyAsync(host);
    }

    [Fact]
    public async Task ConcurrentClients_WithTheSameCpf_ShouldKeepOnlyOne()
    {
        await using var host = await NewHostAsync();

        await ShouldHaveOneWinnerAsync(
            () => CreateClientAsync(host, ValidCpf),
            () => CreateClientAsync(host, ValidCpf));

        (await host.UseAsync<AccessDbContext, int>(c => c.Clients.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentClients_WithTheSamePlate_ShouldKeepOnlyOne()
    {
        await using var host = await NewHostAsync();
        var first = await CreateClientAsync(host, ValidCpf);
        var second = await CreateClientAsync(host, OtherCpf);

        await ShouldHaveOneWinnerAsync(
            () => host.UseAsync<IClientService>(s => s.RegisterVehicleAsync(first, new VehicleRequestDTO("ABC1234"))),
            () => host.UseAsync<IClientService>(s => s.RegisterVehicleAsync(second, new VehicleRequestDTO("ABC1234"))));

        (await host.UseAsync<AccessDbContext, int>(c => c.Vehicles.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Bypass_SecondOwner_ShouldBeRejectedByTheDatabase()
    {
        await using var host = await NewHostAsync();
        await AddOperatorAsync(host, "a@neoparking.test", OperatorRole.Owner);

        var act = () => AddOperatorAsync(host, "b@neoparking.test", OperatorRole.Owner);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Bypass_SecondOpenTicket_ShouldBeRejectedByTheDatabase()
    {
        await using var host = await NewHostAsync();
        await AddOpenTicketAsync(host);

        var act = () => AddOpenTicketAsync(host);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Bypass_SecondActivePolicy_ShouldBeRejectedByTheDatabase()
    {
        await using var host = await NewHostAsync();
        await AddActivePolicyAsync(host);

        var act = () => AddActivePolicyAsync(host);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UpdatingACpf_ToOneInUse_ShouldBeAConflict()
    {
        await using var host = await NewHostAsync();
        await CreateClientAsync(host, ValidCpf);
        var other = await CreateClientAsync(host, OtherCpf);

        var act = () => host.UseAsync<IClientService>(s =>
            s.UpdateClientAsync(other, new ClientUpdateDTO(null, null, ValidCpf)));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task DuplicateOperatorEmail_ShouldBeAConflict()
    {
        await using var host = await NewHostAsync();
        await AddOperatorAsync(host, "a@neoparking.test", OperatorRole.Operator);

        var act = () => AddOperatorAsync(host, "a@neoparking.test", OperatorRole.Operator);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ThePlateMayOpenATicketAgain_AfterTheFirstOneCloses()
    {
        await using var host = await NewHostAsync();
        var first = await host.UseAsync<ITicketService, TicketResponseDTO>(s =>
            s.RegisterEntryAsync(new RegisterEntryRequest("ABC1D23")));
        await host.UseAsync<ITicketService>(s => s.RegisterExitAsync(first.Id, new RegisterExitRequest()));

        await OpenTicketAsync(host);

        (await host.UseAsync<AccessDbContext, int>(c => c.Tickets.CountAsync())).Should().Be(2);
    }

    [Fact]
    public async Task RevisingAPolicy_ShouldCloseThePreviousAndKeepOneActive()
    {
        await using var host = await NewHostAsync();

        await DefinePolicyAsync(host);
        await DefinePolicyAsync(host);

        (await host.UseAsync<BillingDbContext, int>(c => c.PricingPolicies.CountAsync())).Should().Be(2);
        (await host.UseAsync<BillingDbContext, int>(c => c.PricingPolicies.CountAsync(p => p.EffectiveTo == null)))
            .Should().Be(1);
    }

    // Se a segunda definição começar depois do commit da primeira, ela é uma revisão legítima e as
    // duas têm sucesso. A invariante é a política ativa única, e a perda da corrida é um conflito.
    private static async Task ShouldKeepOneActivePolicyAsync(ModuleHost host)
    {
        var outcomes = await Task.WhenAll(
            CaptureAsync(() => DefinePolicyAsync(host)),
            CaptureAsync(() => DefinePolicyAsync(host)));

        outcomes.Should().Contain(e => e == null);
        outcomes.Where(e => e is not null).Should().AllBeOfType<ConflictException>();
        (await host.UseAsync<BillingDbContext, int>(c => c.PricingPolicies.CountAsync(p => p.EffectiveTo == null)))
            .Should().Be(1);
    }

    private static async Task ShouldHaveOneWinnerAsync(Func<Task> first, Func<Task> second)
    {
        var outcomes = await Task.WhenAll(CaptureAsync(first), CaptureAsync(second));

        outcomes.Count(e => e is null).Should().Be(1, "exactly one operation should be recorded");
        outcomes.Single(e => e is not null).Should().BeOfType<ConflictException>();
    }

    private static async Task<Exception?> CaptureAsync(Func<Task> operation)
    {
        try
        {
            // Sai da thread do teste para as duas operações realmente se sobreporem.
            await Task.Run(operation);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static Task CreateOwnerAsync(ModuleHost host, string email) =>
        host.UseAsync<OperatorService>(s => s.CreateOwnerAsync(new CreateOwnerRequest("Owner", email, "Password-123!")));

    private static Task OpenTicketAsync(ModuleHost host) =>
        host.UseAsync<ITicketService>(s => s.RegisterEntryAsync(new RegisterEntryRequest("ABC1D23")));

    private static Task DefinePolicyAsync(ModuleHost host) =>
        host.UseAsync<PricingPolicyService>(s =>
            s.DefineAsync(Guid.NewGuid(), new DefinePricingPolicyRequest(10m, 4m)));

    private static async Task<Guid> CreateClientAsync(ModuleHost host, string cpf)
    {
        var created = await host.UseAsync<IClientService, ClientResponseDTO>(s =>
            s.CreateClientAsync(new ClientRequestDTO("Cliente", PhoneNumberValue, cpf, null)));
        return created.Id;
    }

    private static Task AddOperatorAsync(ModuleHost host, string email, OperatorRole role) =>
        host.UseAsync<IOperatorRepository, ManagementUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(OperatorUser.Create("Name", email, PasswordHash.FromHash(Hash), role));
            await unitOfWork.SaveChangesAsync();
        });

    private static Task AddOpenTicketAsync(ModuleHost host) =>
        host.UseAsync<NeoParking.Access.Application.ITicketRepository, AccessUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(AccessTicket.RegisterEntry(Plate.Create("ABC1D23"), null));
            await unitOfWork.SaveChangesAsync();
        });

    private static Task AddActivePolicyAsync(ModuleHost host) =>
        host.UseAsync<IPricingPolicyRepository, BillingUnitOfWork>(async (repository, unitOfWork) =>
        {
            await repository.AddAsync(PricingPolicy.Define(Guid.NewGuid(), Money.Create(10m), Money.Create(4m)));
            await unitOfWork.SaveChangesAsync();
        });
}
