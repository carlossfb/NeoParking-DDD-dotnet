namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;
using NeoParking.Shared.Kernel.Outbox;

public sealed class ClientService : IClientService
{
    private readonly IClientRepository _repository;
    private readonly IAccessOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public ClientService(
        IClientRepository repository,
        IAccessOutboxRepository outbox,
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
            Document.Create(ClientTypes.Parse(dto.Type), dto.Document));

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

        client.Update(dto.Name, dto.PhoneNumber, DocumentForUpdate(client, dto));
        await _repository.UpdateAsync(client);
        await _unitOfWork.SaveChangesAsync(); 
        return ToResponse(client);
    }

    public async Task DeleteClientAsync(Guid clientId)
    {
        var client = await _repository.GetByIdAsync(clientId)
            ?? throw new NotFoundException(nameof(Client), clientId);

        await _repository.DeleteAsync(client);
        await _unitOfWork.SaveChangesAsync(); 
    }

    // O tipo só muda junto com um documento desse tipo; sem tipo, vale o tipo atual do cliente.
    private static Document? DocumentForUpdate(Client client, ClientUpdateDTO dto)
    {
        var type = dto.Type is null ? client.Type : ClientTypes.Parse(dto.Type);

        if (dto.Document is not null)
            return Document.Create(type, dto.Document);

        return type == client.Type
            ? null
            : throw new DomainException("The document is required when the client type changes.");
    }

    private static ClientResponseDTO ToResponse(Client client) =>
        new(
            client.Id,
            client.Name,
            client.PhoneNumber.Value,
            client.Type.ToString(),
            client.DocumentValue,
            client.Vehicles
                .Select(v => new VehicleResponseDTO(v.Id, v.Plate.Value))
                .ToList());

    public async Task<VehicleResponseDTO> RegisterVehicleAsync(Guid clientId, VehicleRequestDTO dto)
    {
        var client = await _repository.GetByIdAsync(clientId)
            ?? throw new NotFoundException(nameof(Client), clientId);

        var vehicle = client.RegisterVehicle(Plate.Create(dto.Plate));
        await _repository.UpdateAsync(client);
        await _unitOfWork.SaveChangesAsync(); 
        return new VehicleResponseDTO(vehicle.Id, vehicle.Plate.Value);
    }

    public async Task RemoveVehicleAsync(Guid clientId, Guid vehicleId)
    {
        var client = await _repository.GetByIdAsync(clientId)
            ?? throw new NotFoundException(nameof(Client), clientId);

        client.RemoveVehicle(vehicleId);
        await _repository.UpdateAsync(client);
        await _unitOfWork.SaveChangesAsync(); 
    }
}
