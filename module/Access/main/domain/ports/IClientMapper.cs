using main.common.dto;
using main.domain.entity;

namespace main.domain.ports;

public interface IClientMapper
{
    Client RequestDTOToDomain(ClientRequestDTO dto);
    ClientResponseDTO DomainToResponseDTO(Client client);
    Client UpdateClientFromDTO(Client existingClient, ClientUpdateDTO dto);
}