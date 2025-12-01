using main.common.dto;
using main.domain.exception;
using main.domain.ports;

namespace main.application.service
{
    public class ClientServiceImpl : IClientService
    {
        private readonly IClientRepository _clientRepository;
        private readonly IClientMapper _clientMapper;

        public ClientServiceImpl(IClientRepository clientRepository, IClientMapper clientMapper)
        {
            _clientRepository = clientRepository;
            _clientMapper = clientMapper;
        }

        public async Task<ClientResponseDTO> CreateClientAsync(ClientRequestDTO dto)
        {
            var client = _clientMapper.RequestDTOToDomain(dto);
            await _clientRepository.AddAsync(client); 
            return _clientMapper.DomainToResponseDTO(client);
        }

        public async Task DeleteClientAsync(Guid clientId)
        {
            var client = await _clientRepository.GetByIdAsync(clientId);

            if (client is null)
                throw new DomainException("Client not found");

            await _clientRepository.DeleteAsync(client);
        }

        public async Task<IEnumerable<ClientResponseDTO>> GetAllClientsAsync()
        {
            var clients = await _clientRepository.GetAllAsync();
            return clients.Select(_clientMapper.DomainToResponseDTO);
        }

        public async Task<ClientResponseDTO?> GetClientByIdAsync(Guid clientId)
        {
            var client = await _clientRepository.GetByIdAsync(clientId);
            return client is null ? null : _clientMapper.DomainToResponseDTO(client);
        }

        public async Task<ClientResponseDTO> UpdateClientAsync(Guid clientId, ClientUpdateDTO dto)
        {
            var existingClient = await _clientRepository.GetByIdAsync(clientId);

            if (existingClient is null)
                throw new DomainException("Client not found");
            
            // Aplica as mudanças do DTO no cliente existente
            var updatedClient = _clientMapper.UpdateClientFromDTO(existingClient, dto);
            
            await _clientRepository.UpdateAsync(updatedClient);
            return _clientMapper.DomainToResponseDTO(updatedClient);
        }
    }
}