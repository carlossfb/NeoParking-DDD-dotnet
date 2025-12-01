using main.application.dto;

namespace main.domain.ports
{
    public interface IClientService
    {
            // Criar um cliente
    Task<ClientResponseDTO> CreateClientAsync(ClientRequestDTO dto);

    // Obter cliente por ID
    Task<ClientResponseDTO?> GetClientByIdAsync(Guid clientId);

    // Listar todos os clientes
    Task<IEnumerable<ClientResponseDTO>> GetAllClientsAsync();

    // Atualizar dados do cliente
    Task<ClientResponseDTO> UpdateClientAsync(Guid clientId, ClientUpdateDTO dto);

    // Deletar cliente
    Task DeleteClientAsync(Guid clientId);
    }
}