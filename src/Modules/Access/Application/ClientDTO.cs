namespace NeoParking.Access.Application;   
    public sealed record ClientRequestDTO(string Name, string PhoneNumber, string Type, string Document, List<VehicleRequestDTO>? Vehicles = null);
    public sealed record ClientResponseDTO(Guid Id, string Name, string PhoneNumber, string Type, string Document, List<VehicleResponseDTO> Vehicles);
    public sealed record ClientUpdateDTO(
        string? Name,
        string? PhoneNumber,
        string? Type,
        string? Document
    );
    public sealed record ClientSearchRequest(string? Plate = null, string? DocumentType = null, string? Document = null);
    public sealed record ClientDocumentResponseDTO(string Type, string Document);
    public sealed record VehicleRequestDTO(string Plate);
    public sealed record VehicleResponseDTO(Guid Id, string Plate);
