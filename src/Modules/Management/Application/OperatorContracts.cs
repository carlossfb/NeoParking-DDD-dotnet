namespace NeoParking.Management.Application;

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

public sealed record OperatorResponseDTO(Guid Id, string Name, string Email, string Role, bool IsActive);
