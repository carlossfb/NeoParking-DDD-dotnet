namespace NeoParking.Shared.Kernel.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException() { }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.") { }

    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}
