namespace NeoParking.Access.Domain;

using NeoParking.Shared.Kernel.Primitives;

public interface ITicketRepository
{
    Task AddAsync(Ticket ticket);
    Task UpdateAsync(Ticket ticket);
    Task<Ticket?> GetByIdAsync(Guid ticketId);

    /// <summary>Ticket ainda aberto para a placa informada, se existir (evita duas entradas simultâneas).</summary>
    Task<Ticket?> GetOpenByPlateAsync(string plate);

    /// <summary>
    /// Listagem paginada de tickets, filtrável por status, placa (busca parcial) e
    /// período de entrada. Todos os filtros são opcionais.
    /// </summary>
    Task<PagedResult<Ticket>> ListAsync(
        TicketStatus? status,
        string? plate,
        DateTime? entryFrom,
        DateTime? entryTo,
        int page,
        int pageSize);
}
