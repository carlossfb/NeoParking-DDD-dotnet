namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;
using NeoParking.Shared.Kernel.Outbox;

public sealed class ClientService : IClientService
{
    private readonly IClientRepository _repository;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public ClientService(
        IClientRepository repository,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork,
        ICorrelationIdProvider correlationIdProvider)
    {
        _repository = repository;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task<ClientResponseDTO> CreateClientAsync(ClientRequestDTO dto)
    {
        var client = Client.Create(
            dto.Name,
            PhoneNumber.Create(dto.PhoneNumber),
            Cpf.Create(dto.Cpf));

        foreach (var v in dto.Vehicles ?? [])
            client.RegisterVehicle(Plate.Create(v.Plate));

        // Ambos os Add antes do SaveChanges — atomicidade garantida
        await _repository.AddAsync(client);
        await _outbox.AddAsync(OutboxMessage.Create(new ClientRegisteredIntegrationEvent(
            ClientId:      client.Id,
            Name:          client.Name,
            CorrelationId: _correlationIdProvider.GetCorrelationId())));

        await _unitOfWork.SaveChangesAsync();

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
            ?? throw new NotFoundException(nameof(Client), clientId);

        client.Update(dto.Name, dto.PhoneNumber, dto.Cpf);
        await _repository.UpdateAsync(client);
        return ToResponse(client);
    }

    public async Task DeleteClientAsync(Guid clientId)
    {
        var client = await _repository.GetByIdAsync(clientId)
            ?? throw new NotFoundException(nameof(Client), clientId);

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

    public async Task<VehicleResponseDTO> RegisterVehicleAsync(Guid clientId, VehicleRequestDTO dto)
    {
        var client = await _repository.GetByIdAsync(clientId)
            ?? throw new NotFoundException(nameof(Client), clientId);

        var vehicle = client.RegisterVehicle(Plate.Create(dto.Plate));
        await _repository.UpdateAsync(client);
        return new VehicleResponseDTO(vehicle.Id, vehicle.Plate.Value);
    }

    public async Task RemoveVehicleAsync(Guid clientId, Guid vehicleId)
    {
        var client = await _repository.GetByIdAsync(clientId)
            ?? throw new NotFoundException(nameof(Client), clientId);

        client.RemoveVehicle(vehicleId);
        await _repository.UpdateAsync(client);
    }
}
