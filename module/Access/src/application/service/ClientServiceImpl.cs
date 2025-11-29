using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using src.application.dto;
using src.domain.ports;
using src.infrastructure.util;

namespace src.application.service
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

        public Task DeleteClientAsync(Guid clientId)
        {
            throw new NotImplementedException();
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