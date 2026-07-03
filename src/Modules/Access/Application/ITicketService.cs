namespace NeoParking.Access.Application;

using NeoParking.Shared.Kernel.Primitives;

public interface ITicketService
{
    Task<TicketResponseDTO> RegisterEntryAsync(RegisterEntryRequest request);
    Task<TicketResponseDTO> RegisterExitAsync(Guid ticketId, RegisterExitRequest request);
    Task<TicketResponseDTO> GetByIdAsync(Guid ticketId);
    Task<PagedResult<TicketResponseDTO>> ListAsync(ListTicketsRequest request);
}
