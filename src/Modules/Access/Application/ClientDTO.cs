namespace NeoParking.Access.Application;   
    public sealed record ClientRequestDTO(string Name, string PhoneNumber, string Type, string Document, List<VehicleRequestDTO>? Vehicles = null);
    public sealed record ClientResponseDTO(Guid Id, string Name, string PhoneNumber, string Type, string Document, List<VehicleResponseDTO> Vehicles);
    public sealed record ClientUpdateDTO(
        string? Name,
        string? PhoneNumber,
        string? Type,
        string? Document
    );
    public sealed record VehicleRequestDTO(string Plate);
    public sealed record VehicleResponseDTO(Guid Id, string Plate);
