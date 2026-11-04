namespace NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// Credenciais recusadas (HTTP 401). A mensagem é sempre a mesma para não revelar
/// se o e-mail existe, se a senha está errada ou se a conta está inativa.
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid credentials.") { }
}
