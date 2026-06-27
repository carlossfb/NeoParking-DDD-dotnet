namespace NeoParking.Access.Application;

public interface IClientService
{
    Task<ClientResponseDTO> CreateClientAsync(ClientRequestDTO dto);
    Task<ClientResponseDTO?> GetClientByIdAsync(Guid clientId);
    Task<IEnumerable<ClientResponseDTO>> GetAllClientsAsync();
    Task<ClientResponseDTO> UpdateClientAsync(Guid clientId, ClientUpdateDTO dto);
    Task DeleteClientAsync(Guid clientId);
}