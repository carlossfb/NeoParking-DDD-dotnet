namespace NeoParking.Access.Infrastructure;

using Microsoft.Extensions.Configuration;

/// <summary>
/// As duas chaves do documento, lidas do ambiente ou do .env. O boot falha se uma delas faltar
/// ou for inválida, e as mensagens nunca trazem o valor.
/// </summary>
public sealed class DocumentProtectionKeys
{
    public const string EncryptionKeyName = "DOCUMENT_ENCRYPTION_KEY";
    public const string HmacKeyName = "DOCUMENT_HMAC_KEY";
    public const int KeyBytes = 32;

    public byte[] Encryption { get; }
    public byte[] Hmac { get; }

    private DocumentProtectionKeys(byte[] encryption, byte[] hmac)
    {
        Encryption = encryption;
        Hmac = hmac;
    }

    /// <summary>Só para <c>dotnet ef</c>, que monta o modelo sem ler nem gravar clientes.</summary>
    public static DocumentProtectionKeys ForDesignTime() =>
        new(System.Security.Cryptography.RandomNumberGenerator.GetBytes(KeyBytes),
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(KeyBytes));

    public static DocumentProtectionKeys FromConfiguration(IConfiguration configuration)
    {
        var encryption = Read(EncryptionKeyName, configuration[EncryptionKeyName]);
        var hmac = Read(HmacKeyName, configuration[HmacKeyName]);

        if (encryption.AsSpan().SequenceEqual(hmac))
            throw new InvalidOperationException($"{EncryptionKeyName} and {HmacKeyName} must be different.");

        return new DocumentProtectionKeys(encryption, hmac);
    }

    private static byte[] Read(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{name} is not set. Define it in the environment or in .env (see .env.example).");

        try
        {
            var bytes = Convert.FromHexString(value.Trim());
            if (bytes.Length == KeyBytes)
                return bytes;
        }
        catch (FormatException)
        {
            // Mesma mensagem do tamanho errado, sem repetir o valor.
        }

        throw new InvalidOperationException(
            $"{name} must have {KeyBytes * 2} hexadecimal characters ({KeyBytes} bytes). Generate it with: openssl rand -hex {KeyBytes}");
    }
}
