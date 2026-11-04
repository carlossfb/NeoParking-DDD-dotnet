namespace NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// O solicitante está identificado, mas não tem permissão para a ação (HTTP 403).
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
