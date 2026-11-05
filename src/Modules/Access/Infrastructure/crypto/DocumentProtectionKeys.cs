namespace NeoParking.Access.Infrastructure;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

/// <summary>
/// As chaves dos dados do cliente, lidas do ambiente ou do .env: a de cifra atual, com a versão dela, as anteriores
/// que ainda precisam ser lidas, e a do HMAC. O boot falha se uma delas faltar ou for inválida, e as mensagens
/// nunca trazem o valor.
/// </summary>
public sealed partial class DocumentProtectionKeys
{
    public const string EncryptionKeyName = "DOCUMENT_ENCRYPTION_KEY";
    public const string KeyVersionName = "DOCUMENT_ENCRYPTION_KEY_VERSION";
    public const string PreviousKeysName = "DOCUMENT_ENCRYPTION_PREVIOUS_KEYS";
    public const string HmacKeyName = "DOCUMENT_HMAC_KEY";
    public const int KeyBytes = 32;

    private readonly Dictionary<string, byte[]> _encryptionKeys;

    public byte[] Encryption { get; }
    public string CurrentVersion { get; }
    public byte[] Hmac { get; }

    private DocumentProtectionKeys(byte[] encryption, int version, Dictionary<int, byte[]> previous, byte[] hmac)
    {
        Encryption = encryption;
        CurrentVersion = Label(version);
        Hmac = hmac;
        _encryptionKeys = previous.ToDictionary(p => Label(p.Key), p => p.Value);
        _encryptionKeys[CurrentVersion] = encryption;
    }

    public byte[]? ForVersion(string version) => _encryptionKeys.GetValueOrDefault(version);

    public static bool IsVersion(string value) => VersionPattern().IsMatch(value);

    /// <summary>Só para <c>dotnet ef</c>, que monta o modelo sem ler nem gravar clientes.</summary>
    public static DocumentProtectionKeys ForDesignTime() =>
        new(RandomNumberGenerator.GetBytes(KeyBytes), 1, [], RandomNumberGenerator.GetBytes(KeyBytes));

    public static DocumentProtectionKeys FromConfiguration(IConfiguration configuration)
    {
        var encryption = Read(EncryptionKeyName, configuration[EncryptionKeyName]);
        var hmac = Read(HmacKeyName, configuration[HmacKeyName]);
        var version = ReadVersion(configuration[KeyVersionName]);
        var previous = ReadPrevious(configuration[PreviousKeysName], version);

        if (encryption.AsSpan().SequenceEqual(hmac))
            throw new InvalidOperationException($"{EncryptionKeyName} and {HmacKeyName} must be different.");

        var all = previous.Values.Append(encryption).Append(hmac).ToList();
        if (all.Select(Convert.ToHexString).Distinct().Count() != all.Count)
            throw new InvalidOperationException(
                $"The keys in {PreviousKeysName}, {EncryptionKeyName} and {HmacKeyName} must be different.");

        return new DocumentProtectionKeys(encryption, version, previous, hmac);
    }

    private static string Label(int version) => $"v{version.ToString(CultureInfo.InvariantCulture)}";

    private static int ReadVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 1;

        return int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var version) && version >= 1
            ? version
            : throw new InvalidOperationException($"{KeyVersionName} must be a positive whole number.");
    }

    // Formato: versão:chave, separados por vírgula. Exemplo com o valor encurtado: 1:ab12...,2:cd34...
    private static Dictionary<int, byte[]> ReadPrevious(string? value, int currentVersion)
    {
        var previous = new Dictionary<int, byte[]>();
        if (string.IsNullOrWhiteSpace(value))
            return previous;

        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = entry.IndexOf(':');
            if (separator <= 0
                || !int.TryParse(entry[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var version)
                || version < 1)
                throw new InvalidOperationException($"{PreviousKeysName} must list version:key pairs separated by commas.");

            if (version == currentVersion)
                throw new InvalidOperationException($"{PreviousKeysName} cannot list the current version.");

            if (!previous.TryAdd(version, Read(PreviousKeysName, entry[(separator + 1)..])))
                throw new InvalidOperationException($"{PreviousKeysName} lists the same version twice.");
        }

        return previous;
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

    [GeneratedRegex("^v[0-9]{1,9}$")]
    private static partial Regex VersionPattern();
}
