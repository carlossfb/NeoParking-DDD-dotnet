namespace NeoParking.Access.Domain;

using NeoParking.Shared.Kernel.Exceptions;

public enum ClientType
{
    Individual,
    Company
}

public static class ClientTypes
{
    public static ClientType Parse(string? value)
    {
        if (string.Equals(value, nameof(ClientType.Individual), StringComparison.OrdinalIgnoreCase))
            return ClientType.Individual;

        if (string.Equals(value, nameof(ClientType.Company), StringComparison.OrdinalIgnoreCase))
            return ClientType.Company;

        throw new DomainException("The client type must be Individual or Company.");
    }
}
