namespace main.common.dto
{
    public record ClientUpdateDTO(
        string? Name,
        string? PhoneNumber,
        string? Cpf
    );
}