namespace NeoParking.Management.Domain.Vo;

using NeoParking.Shared.Kernel.Exceptions;

/// <summary>
/// Value object que encapsula o hash da senha.
/// Nunca armazena a senha em texto plano — apenas o hash gerado pelo IPasswordHasher.
/// </summary>
public sealed class PasswordHash
{
    public string Value { get; }

    private PasswordHash(string value) => Value = value;

    /// <summary>
    /// Cria a partir de um hash já gerado (BCrypt, etc).
    /// Usado pelo serviço de aplicação e pelo EF Core na reconstituição.
    /// </summary>
    public static PasswordHash FromHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new DomainException("Password hash cannot be empty.");

        return new PasswordHash(hash);
    }

    public override string ToString() => Value;

    public override bool Equals(object? obj)
        => obj is PasswordHash other && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();
}
