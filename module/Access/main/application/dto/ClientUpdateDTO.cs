namespace main.application.dto
{
    public record ClientUpdateDTO(
        string? Name,
        string? PhoneNumber,
        string? Cpf
    );
}