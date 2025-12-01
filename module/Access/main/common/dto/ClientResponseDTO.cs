namespace main.common.dto
{
    public sealed record ClientResponseDTO(Guid Id, string Name, string PhoneNumber, string Cpf, List<VehicleResponseDTO> Vehicles);
}