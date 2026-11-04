namespace NeoParking.Api.Extensions;

using System.Text;

/// <summary>
/// Recusa no boot um Jwt:Secret que permitiria forjar tokens. Fora de Development, exige
/// pelo menos 32 bytes e nenhum marcador de placeholder. As mensagens nunca contêm o valor.
/// </summary>
public static class JwtSecretValidator
{
    public const int MinimumBytes = 32;

    private static readonly string[] PlaceholderMarkers =
    {
        "CHANGE_ME", "CHANGE-ME", "CHANGEME", "PLACEHOLDER", "REPLACE_ME", "REPLACE-ME", "YOUR_", "YOUR-", "EXAMPLE"
    };

    public static void Validate(string? secret, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Jwt:Secret not configured.");

        if (isDevelopment)
            return;

        if (PlaceholderMarkers.Any(marker => secret.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Jwt:Secret is a placeholder. Configure a real secret for this environment.");

        if (Encoding.UTF8.GetByteCount(secret) < MinimumBytes)
            throw new InvalidOperationException($"Jwt:Secret must have at least {MinimumBytes} bytes.");
    }
}
