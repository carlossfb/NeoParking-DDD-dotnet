namespace NeoParking.Management.Application;

using NeoParking.Management.Domain;

/// <summary>
/// Abstração para hash de senha. Implementação usa BCrypt na infra.
/// Mantido na Application para que o Domain não dependa de BCrypt.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainPassword);
    bool Verify(string plainPassword, string hash);
}

// ── requests ─────────────────────────────────────────────────────────────────

public sealed record CreateOwnerRequest(string Name, string Email, string Password);

public sealed record CreateOperatorRequest(string Name, string Email, string Password, string Role);

public sealed record UpdatePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record LoginRequest(string Email, string Password);

// ── responses ────────────────────────────────────────────────────────────────

public enum ChangePasswordResult
{
    Changed,
    WrongCurrentPassword,
    InvalidNewPassword
}

public sealed record OperatorResponseDTO(Guid Id, string Name, string Email, string Role, bool IsActive);

/// <summary>Nomes dos papéis, para quem fica fora do módulo e não usa o Domain.</summary>
public static class OperatorRoleNames
{
    public const string Operator = nameof(OperatorRole.Operator);
    public const string Admin = nameof(OperatorRole.Admin);
    public const string Owner = nameof(OperatorRole.Owner);
}
