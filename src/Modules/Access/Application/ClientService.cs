namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Exceptions;

public sealed class ClientService : IClientService
{
    private readonly IClientRepository _repository;

    public ClientService(IClientRepository repository)
    {
        _repository = repository;
    }

    public async Task<ClientResponseDTO> CreateClientAsync(ClientRequestDTO dto)
    {
        var client = Client.Create(
            dto.Name,
            PhoneNumber.Create(dto.PhoneNumber),
            Cpf.Create(dto.Cpf));

        foreach (var v in dto.Vehicles ?? [])
            client.RegisterVehicle(Plate.Create(v.Plate));

        await _repository.AddAsync(client);
        return ToResponse(client);
    }

    public async Task<ClientResponseDTO?> GetClientByIdAsync(Guid clientId)
    {
        var client = await _repository.GetByIdAsync(clientId);
        return client is null ? null : ToResponse(client);
    }

    public async Task<IEnumerable<ClientResponseDTO>> GetAllClientsAsync()
    {
        var clients = await _repository.GetAllAsync();
        return clients.Select(ToResponse);
    }

    public async Task<ClientResponseDTO> UpdateClientAsync(Guid clientId, ClientUpdateDTO dto)
    {
        var client = await _repository.GetByIdAsync(clientId)
            ?? throw new DomainException("Client not found");

        client.Update(dto.Name, dto.PhoneNumber, dto.Cpf);

        await _repository.UpdateAsync(client);
        return ToResponse(client);
    }

    public async Task DeleteClientAsync(Guid clientId)
    {
        var client = await _repository.GetByIdAsync(clientId)
            ?? throw new DomainException("Client not found");

        await _repository.DeleteAsync(client);
    }

    private static ClientResponseDTO ToResponse(Client client) =>
        new(
            client.Id,
            client.Name,
            client.PhoneNumber.Value,
            client.Cpf.Document,
            client.Vehicles
                .Select(v => new VehicleResponseDTO(v.Id, v.Plate.Value))
                .ToList());
}