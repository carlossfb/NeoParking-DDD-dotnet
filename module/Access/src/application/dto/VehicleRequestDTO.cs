using src.domain.vo;

namespace src.application.dto
{
    public sealed record VehicleRequestDTO(Guid Id, string Plate);
}