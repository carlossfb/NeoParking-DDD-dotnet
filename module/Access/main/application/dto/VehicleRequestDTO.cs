using main.domain.vo;

namespace main.application.dto
{
    public sealed record VehicleRequestDTO(Guid Id, string Plate);
}