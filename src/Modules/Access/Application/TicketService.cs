namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;
using NeoParking.Shared.Kernel.Outbox;
using NeoParking.Shared.Kernel.Primitives;

/// <summary>
/// Orquestra o ciclo de vida de um Ticket (entrada/saída na cancela). É Access quem
/// registra esses eventos — o Billing apenas reage via <see cref="TicketOpenedIntegrationEvent"/>
/// e <see cref="TicketClosedIntegrationEvent"/> para calcular e congelar a Fee.
/// </summary>
public sealed class TicketService : ITicketService
{
    private readonly ITicketRepository _tickets;
    private readonly IAccessOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public TicketService(
        ITicketRepository tickets,
        IAccessOutboxRepository outbox,
        IUnitOfWork unitOfWork,
        ICorrelationIdProvider correlationIdProvider)
    {
        _tickets = tickets;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task<TicketResponseDTO> RegisterEntryAsync(RegisterEntryRequest request)
    {
        var plate = Plate.Create(request.Plate);

        var existingOpen = await _tickets.GetOpenByPlateAsync(plate.Value);
        if (existingOpen is not null)
            throw new ConflictException("This vehicle already has an open ticket.");

        var ticket = Ticket.RegisterEntry(plate, request.ClientId);

        // Ambos os Add antes do SaveChanges — atomicidade garantida (mesmo padrão do ClientService)
        await _tickets.AddAsync(ticket);
        await _outbox.AddAsync(OutboxMessage.Create(new TicketOpenedIntegrationEvent(
            TicketId:      ticket.Id,
            Plate:         ticket.Plate.Value,
            ClientId:      ticket.ClientId,
            EntryAt:       ticket.EntryAt,
            CorrelationId: _correlationIdProvider.GetCorrelationId())));

        await _unitOfWork.SaveChangesAsync();

        return ToResponse(ticket);
    }

    public async Task<TicketResponseDTO> RegisterExitAsync(Guid ticketId, RegisterExitRequest request)
    {
        var ticket = await _tickets.GetByIdAsync(ticketId)
            ?? throw new NotFoundException(nameof(Ticket), ticketId);

        ticket.RegisterExit(request.ExitAt);

        return await PersistExitAsync(ticket);
    }

    public async Task<TicketResponseDTO> RegisterExitByCodeAsync(string code, RegisterExitRequest request)
    {
        var ticket = await FindByCodeAsync(code);

        ticket.RegisterExit(request.ExitAt, ExitMethod.Code);

        return await PersistExitAsync(ticket);
    }

    public async Task<TicketResponseDTO> RegisterExitByPlateAsync(
        string plate, RegisterExitByPlateRequest request, Guid operatorId)
    {
        var ticket = await _tickets.GetOpenByPlateAsync(Plate.Create(plate).Value)
            ?? throw new NotFoundException("No open ticket was found for this plate.");

        ticket.RegisterLostTicketExit(operatorId, request.Reason, request.ExitAt);

        return await PersistExitAsync(ticket);
    }

    public async Task<TicketResponseDTO> GetByCodeAsync(string code) =>
        ToResponse(await FindByCodeAsync(code));

    public async Task<TicketResponseDTO> GetByIdAsync(Guid ticketId)
    {
        var ticket = await _tickets.GetByIdAsync(ticketId)
            ?? throw new NotFoundException(nameof(Ticket), ticketId);

        return ToResponse(ticket);
    }

    /// <summary>
    /// Listagem paginada de tickets — visão operacional geral (abertos e fechados),
    /// filtrável por status, placa e período de entrada.
    /// </summary>
    public async Task<PagedResult<TicketResponseDTO>> ListAsync(ListTicketsRequest request)
    {
        var result = await _tickets.ListAsync(
            request.Status, request.Plate, request.EntryFrom, request.EntryTo,
            request.Page, request.PageSize);

        return new PagedResult<TicketResponseDTO>(
            result.Items.Select(ToResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    private async Task<Ticket> FindByCodeAsync(string code)
    {
        // Um código malformado é um código que não existe; ele também não é repetido na mensagem.
        var ticket = TicketCode.TryParse(code, out var parsed)
            ? await _tickets.GetByCodeAsync(parsed!)
            : null;

        return ticket ?? throw new NotFoundException("No ticket was found for this code.");
    }

    private async Task<TicketResponseDTO> PersistExitAsync(Ticket ticket)
    {
        await _tickets.UpdateAsync(ticket);
        await _outbox.AddAsync(OutboxMessage.Create(new TicketClosedIntegrationEvent(
            TicketId:      ticket.Id,
            Plate:         ticket.Plate.Value,
            ExitAt:        ticket.ExitAt!.Value,
            CorrelationId: _correlationIdProvider.GetCorrelationId())));

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (ConflictException)
        {
            // Outra saída encerrou o ticket depois da leitura; o evento dela é o único publicado.
            throw new DomainException("Ticket is already closed.");
        }

        return ToResponse(ticket);
    }

    private static TicketResponseDTO ToResponse(Ticket ticket) =>
        new(ticket.Id, ticket.Code.Value, ticket.Plate.Value, ticket.ClientId, ticket.EntryAt, ticket.ExitAt, ticket.Status.ToString(), ticket.ExitMethod?.ToString());
}
