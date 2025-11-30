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

        public Task<IEnumerable<ClientResponseDTO>> GetAllClientsAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<ClientResponseDTO?> GetClientByIdAsync(Guid clientId)
        {
            var client = await _clientRepository.GetByIdAsync(clientId);
            return client is null ? null : ClientMapper.DomainToResponseDTO(client);
        }

        public Task<ClientResponseDTO> UpdateClientAsync(Guid clientId, ClientRequestDTO dto)
        {
            throw new NotImplementedException();
        }
    }
}