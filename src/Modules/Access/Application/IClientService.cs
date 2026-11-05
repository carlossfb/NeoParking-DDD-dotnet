namespace NeoParking.Access.Application;

using NeoParking.Shared.Kernel.Primitives;

public interface IClientService
{
    Task<ClientResponseDTO> CreateClientAsync(ClientRequestDTO dto);
    Task<ClientResponseDTO?> GetClientByIdAsync(Guid clientId);
    Task<ClientDocumentResponseDTO> RevealDocumentAsync(Guid clientId, Guid operatorId);
    Task<PagedResult<ClientResponseDTO>> ListClientsAsync(PageRequest page);
    Task<ClientResponseDTO?> SearchClientAsync(ClientSearchRequest request);
    Task<ClientResponseDTO> UpdateClientAsync(Guid clientId, ClientUpdateDTO dto);
    Task DeleteClientAsync(Guid clientId);
    Task<VehicleResponseDTO> RegisterVehicleAsync(Guid clientId, VehicleRequestDTO dto);
    Task RemoveVehicleAsync(Guid clientId, Guid vehicleId);
}