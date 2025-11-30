using main.application.dto;
using main.domain.exception;
using main.domain.ports;
using main.infrastructure.util;

namespace main.application.service
{
    public class ClientServiceImpl : IClientService
    {
        private readonly IClientRepository _clientRepository;

        public ClientServiceImpl(IClientRepository clientRepository)
        {
            _clientRepository = clientRepository;
        }

        public async Task<ClientResponseDTO> CreateClientAsync(ClientRequestDTO dto)
        {
            var client = ClientMapper.RequestDTOToDomain(dto);
            await _clientRepository.AddAsync(client); 
            return ClientMapper.DomainToResponseDTO(client);
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
            return clients.Select(ClientMapper.DomainToResponseDTO);
        }

        public async Task<ClientResponseDTO?> GetClientByIdAsync(Guid clientId)
        {
            var client = await _clientRepository.GetByIdAsync(clientId);
            return client is null ? null : ClientMapper.DomainToResponseDTO(client);
        }

        public async Task<ClientResponseDTO> UpdateClientAsync(Guid clientId, ClientRequestDTO dto)
        {
            var existingClient = await _clientRepository.GetByIdAsync(clientId);

            if (existingClient is null)
                throw new DomainException("Client not found");
            
            await _clientRepository.UpdateAsync(existingClient);
            return ClientMapper.DomainToResponseDTO(existingClient);
        }
    }
}