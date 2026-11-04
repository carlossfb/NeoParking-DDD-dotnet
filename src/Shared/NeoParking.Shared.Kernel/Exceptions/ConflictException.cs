namespace NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// A operação viola uma regra de unicidade (HTTP 409). A mensagem chega ao cliente da API:
/// nunca coloque nela detalhes do banco.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, Exception innerException) : base(message, innerException) { }
}
