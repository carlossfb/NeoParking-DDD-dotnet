namespace NeoParking.Integration.Tests.Tickets;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Access.Infrastructure;
using NeoParking.Integration.Tests.Support;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Outbox;
using AccessUnitOfWork = NeoParking.Access.Application.IUnitOfWork;

/// <summary>C2: o índice garante o código único, e duas saídas nunca publicam dois eventos.</summary>
[Collection(MySqlCollection.Name)]
[Trait("Category", "Integration")]
public sealed class TicketCodeTests
{
    private const string PlateValue = "ABC1D23";

    private readonly MySqlFixture _mysql;

    public TicketCodeTests(MySqlFixture mysql) => _mysql = mysql;

    private async Task<ModuleHost> NewHostAsync() =>
        await ModuleHost.CreateMigratedAsync(await _mysql.CreateDatabasesAsync());

    private static Task<TicketResponseDTO> OpenAsync(ModuleHost host, string plate = PlateValue) =>
        host.UseAsync<ITicketService, TicketResponseDTO>(s => s.RegisterEntryAsync(new RegisterEntryRequest(plate)));

    private static Task<int> ClosedEventsAsync(ModuleHost host) =>
        host.UseAsync<AccessDbContext, int>(c =>
            c.OutboxMessages.CountAsync(m => m.Type == typeof(TicketClosedIntegrationEvent).FullName));

    [Fact]
    public async Task DuplicateCode_ShouldBeRejectedByTheIndex()
    {
        await using var host = await NewHostAsync();
        var first = await OpenAsync(host);

        var act = () => host.UseAsync<ITicketRepository, AccessUnitOfWork>(async (tickets, unitOfWork) =>
        {
            await tickets.AddAsync(Ticket.Reconstitute(
                Guid.NewGuid(), TicketCode.Create(first.Code), Plate.Create("XYZ9876"), null,
                DateTime.UtcNow, null, TicketStatus.Open));
            await unitOfWork.SaveChangesAsync();
        });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ByCode_ShouldFindTheTicket_IgnoringCase()
    {
        await using var host = await NewHostAsync();
        var opened = await OpenAsync(host);

        var found = await host.UseAsync<ITicketService, TicketResponseDTO>(s =>
            s.GetByCodeAsync(opened.Code.ToLowerInvariant()));

        found.Id.Should().Be(opened.Id);
    }

    [Fact]
    public async Task ExitByPlate_ShouldPersistMethodReasonAndAuthor()
    {
        await using var host = await NewHostAsync();
        var opened = await OpenAsync(host);
        var operatorId = Guid.NewGuid();

        await host.UseAsync<ITicketService>(s => s.RegisterExitByPlateAsync(
            PlateValue, new RegisterExitByPlateRequest("Ticket perdido"), operatorId));

        var stored = await host.UseAsync<AccessDbContext, Ticket>(c =>
            c.Tickets.AsNoTracking().SingleAsync(t => t.Id == opened.Id));
        stored.Status.Should().Be(TicketStatus.Closed);
        stored.ExitMethod.Should().Be(ExitMethod.Plate);
        stored.ExitReason.Should().Be("Ticket perdido");
        stored.ExitOperatorId.Should().Be(operatorId);
        stored.Code.Value.Should().Be(opened.Code);
    }

    [Fact]
    public async Task ExitWithAStaleTicket_ShouldNotCloseTwice_NorPublishTwice()
    {
        await using var host = await NewHostAsync();
        var opened = await OpenAsync(host);

        // Determinístico: a segunda saída carregou o ticket ainda aberto, antes do commit da primeira.
        var act = () => host.UseAsync<ITicketRepository, IAccessOutboxRepository, AccessUnitOfWork>(
            async (tickets, outbox, unitOfWork) =>
            {
                var stale = (await tickets.GetByIdAsync(opened.Id))!;

                await host.UseAsync<ITicketService>(s => s.RegisterExitAsync(opened.Id, new RegisterExitRequest()));

                stale.RegisterExit(DateTime.UtcNow.AddMinutes(1), ExitMethod.Code);
                await tickets.UpdateAsync(stale);
                await outbox.AddAsync(OutboxMessage.Create(new TicketClosedIntegrationEvent(
                    stale.Id, stale.Plate.Value, stale.ExitAt!.Value, Guid.NewGuid())));
                await unitOfWork.SaveChangesAsync();
            });

        await act.Should().ThrowAsync<ConflictException>();
        (await ClosedEventsAsync(host)).Should().Be(1);
        var stored = await host.UseAsync<AccessDbContext, Ticket>(c =>
            c.Tickets.AsNoTracking().SingleAsync(t => t.Id == opened.Id));
        stored.ExitMethod.Should().Be(ExitMethod.TicketId);
    }

    [Fact]
    public async Task ConcurrentExits_OfTheSameTicket_ShouldPublishASingleEvent()
    {
        await using var host = await NewHostAsync();

        for (var round = 0; round < 10; round++)
        {
            var plate = $"ABC{1000 + round}";
            var opened = await OpenAsync(host, plate);
            var before = await ClosedEventsAsync(host);

            var outcomes = await Task.WhenAll(
                CaptureAsync(() => host.UseAsync<ITicketService>(s =>
                    s.RegisterExitByCodeAsync(opened.Code, new RegisterExitRequest()))),
                CaptureAsync(() => host.UseAsync<ITicketService>(s =>
                    s.RegisterExitAsync(opened.Id, new RegisterExitRequest()))),
                CaptureAsync(() => host.UseAsync<ITicketService>(s =>
                    s.RegisterExitByPlateAsync(plate, new RegisterExitByPlateRequest("Ticket perdido"), Guid.NewGuid()))));

            outcomes.Count(e => e is null).Should().Be(1, "only one exit may win");
            outcomes.Where(e => e is not null).Should().AllSatisfy(e =>
                (e is DomainException or NotFoundException).Should().BeTrue($"unexpected {e}"));
            (await ClosedEventsAsync(host)).Should().Be(before + 1);
        }
    }

    private static async Task<Exception?> CaptureAsync(Func<Task> operation)
    {
        try
        {
            await Task.Run(operation);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
