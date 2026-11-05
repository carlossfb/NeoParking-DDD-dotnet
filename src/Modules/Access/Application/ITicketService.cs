namespace NeoParking.Access.Application;

using NeoParking.Shared.Kernel.Primitives;

public interface ITicketService
{
    Task<TicketResponseDTO> RegisterEntryAsync(RegisterEntryRequest request);
    Task<TicketResponseDTO> RegisterExitAsync(Guid ticketId, RegisterExitRequest request);
    Task<TicketResponseDTO> RegisterExitByCodeAsync(string code, RegisterExitRequest request);
    Task<TicketResponseDTO> RegisterExitByPlateAsync(string plate, RegisterExitByPlateRequest request, Guid operatorId);
    Task<TicketResponseDTO> GetByIdAsync(Guid ticketId);
    Task<TicketResponseDTO> GetByCodeAsync(string code);
    Task<TicketResponseDTO> GetOpenByPlateAsync(OpenTicketByPlateRequest request);
    Task<PagedResult<TicketResponseDTO>> ListAsync(ListTicketsRequest request);
}
