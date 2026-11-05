namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;
using NeoParking.Shared.Kernel.Outbox;
using NeoParking.Shared.Kernel.Primitives;

public sealed class ClientService : IClientService
{
    private readonly IClientRepository _repository;
    private readonly IAccessOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IDocumentProtector _documentProtector;

    public ClientService(
        IClientRepository repository,
        IAccessOutboxRepository outbox,
        IUnitOfWork unitOfWork,
        ICorrelationIdProvider correlationIdProvider,
        IDocumentProtector documentProtector)
    {
        _repository = repository;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _correlationIdProvider = correlationIdProvider;
        _documentProtector = documentProtector;
    }

    public async Task<ClientResponseDTO> CreateClientAsync(ClientRequestDTO dto)
    {
        var client = Client.Create(
            dto.Name,
            PhoneNumber.Create(dto.PhoneNumber),
            _documentProtector.Protect(Document.Create(ClientTypes.Parse(dto.Type), dto.Document)));

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

    /// <summary>Único caminho até o documento completo: só devolve depois de gravar quem o leu.</summary>
    public async Task<ClientDocumentResponseDTO> RevealDocumentAsync(Guid clientId, Guid operatorId)
    {
        var client = await _repository.GetByIdAsync(clientId)
            ?? throw new NotFoundException(nameof(Client), clientId);

        var document = _documentProtector.Reveal(client.Document);

        await _repository.AddDocumentAccessAsync(ClientDocumentAccess.Record(clientId, operatorId));
        await _unitOfWork.SaveChangesAsync();

        return new ClientDocumentResponseDTO(document.Type.ToString(), document.Value);
    }

    /// <summary>Não confere se o cliente existe: a auditoria sobrevive à exclusão dele.</summary>
    public async Task<PagedResult<ClientDocumentAccessDTO>> ListDocumentAccessesAsync(Guid clientId, PageRequest page)
    {
        var result = await _repository.ListDocumentAccessesAsync(clientId, page);

        return new PagedResult<ClientDocumentAccessDTO>(
            result.Items.Select(a => new ClientDocumentAccessDTO(a.OperatorId, a.AccessedAt)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    public async Task<PagedResult<ClientResponseDTO>> ListClientsAsync(PageRequest page)
    {
        var result = await _repository.ListAsync(page);

        return new PagedResult<ClientResponseDTO>(
            result.Items.Select(ToResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    /// <summary>
    /// Busca exata por placa ou por documento, os dois campos que continuam pesquisáveis:
    /// a placa fica em claro, e o documento é comparado pelo HMAC.
    /// </summary>
    public async Task<ClientResponseDTO?> SearchClientAsync(ClientSearchRequest request)
    {
        var byPlate = !string.IsNullOrWhiteSpace(request.Plate);
        var byDocument = request.DocumentType is not null || request.Document is not null;

        if (byPlate == byDocument)
            throw new DomainException("Provide either a plate or a document.");

        if (byPlate)
        {
            var found = await _repository.GetByPlateAsync(Plate.Create(request.Plate!));
            return found is null ? null : ToResponse(found);
        }

        if (request.Document is null)
            throw new DomainException("The document is required.");

        var document = Document.Create(ClientTypes.Parse(request.DocumentType), request.Document);
        var client = await _repository.GetByDocumentHmacAsync(_documentProtector.Protect(document).Hmac);
        return client is null ? null : ToResponse(client);
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
    private ProtectedDocument? DocumentForUpdate(Client client, ClientUpdateDTO dto)
    {
        var type = dto.Type is null ? client.Type : ClientTypes.Parse(dto.Type);

        if (dto.Document is not null)
            return _documentProtector.Protect(Document.Create(type, dto.Document));

        return type == client.Type
            ? null
            : throw new DomainException("The document is required when the client type changes.");
    }

    private ClientResponseDTO ToResponse(Client client) =>
        new(
            client.Id,
            client.Name,
            client.PhoneNumber.Value,
            client.Type.ToString(),
            _documentProtector.Reveal(client.Document).Masked,
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
