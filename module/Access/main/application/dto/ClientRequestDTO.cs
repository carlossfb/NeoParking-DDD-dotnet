
namespace main.application.dto
{
    public sealed record ClientRequestDTO(string Name, string PhoneNumber, string Cpf, List<VehicleRequestDTO>? Vehicles = null);
}