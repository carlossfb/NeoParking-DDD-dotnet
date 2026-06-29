namespace NeoParking.Management.Domain;

using NeoParking.Management.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Primitives;

/// <summary>
/// Aggregate root do Management BC.
/// Representa um usuário operador do sistema de estacionamento (Operator, Admin ou Owner).
/// Owner é único e não pode ser desativado, rebaixado ou ter sua role alterada.
/// </summary>
public sealed class OperatorUser : Entity
{
    public string Name         { get; private set; }
    public string Email        { get; private set; }
    public PasswordHash PasswordHash { get; private set; }
    public OperatorRole Role   { get; private set; }
    public bool IsActive       { get; private set; }
    public DateTime CreatedAt  { get; private set; }

    // Construtor para EF Core
    private OperatorUser() : base(Guid.Empty)
    {
        Name         = string.Empty;
        Email        = string.Empty;
        PasswordHash = null!;
    }

    private OperatorUser(string name, string email, PasswordHash passwordHash, OperatorRole role) : base()
    {
        ValidateName(name);
        ValidateEmail(email);

        Name         = name;
        Email        = email.ToLowerInvariant();
        PasswordHash = passwordHash;
        Role         = role;
        IsActive     = true;
        CreatedAt    = DateTime.UtcNow;
    }

    private OperatorUser(Guid id, string name, string email, PasswordHash passwordHash,
        OperatorRole role, bool isActive, DateTime createdAt) : base(id)
    {
        Name         = name;
        Email        = email;
        PasswordHash = passwordHash;
        Role         = role;
        IsActive     = isActive;
        CreatedAt    = createdAt;
    }

    // ── factory methods ──────────────────────────────────────────────────────

    public static OperatorUser Create(string name, string email, PasswordHash passwordHash, OperatorRole role)
        => new(name, email, passwordHash, role);

    /// <summary>Reconstitui a partir do banco sem gerar novo Id ou CreatedAt.</summary>
    public static OperatorUser Reconstitute(Guid id, string name, string email,
        PasswordHash passwordHash, OperatorRole role, bool isActive, DateTime createdAt)
        => new(id, name, email, passwordHash, role, isActive, createdAt);

    // ── comportamentos ───────────────────────────────────────────────────────

    public void PromoteToAdmin()
    {
        if (Role == OperatorRole.Owner)
            throw new DomainException("Owner role cannot be changed.");

        Role = OperatorRole.Admin;
    }

    public void DemoteToOperator()
    {
        if (Role == OperatorRole.Owner)
            throw new DomainException("Owner role cannot be changed.");

        Role = OperatorRole.Operator;
    }

    public void Activate()   => IsActive = true;

    public void Deactivate()
    {
        if (Role == OperatorRole.Owner)
            throw new DomainException("Owner cannot be deactivated.");

        IsActive = false;
    }

    public void UpdatePassword(PasswordHash newHash)
    {
        PasswordHash = newHash ?? throw new DomainException("Password hash is required.");
    }

    // ── validações ───────────────────────────────────────────────────────────

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Name is required.");
    }

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("Invalid email address.");
    }
}
