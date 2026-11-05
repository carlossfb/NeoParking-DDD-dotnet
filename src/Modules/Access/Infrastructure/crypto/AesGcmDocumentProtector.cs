namespace NeoParking.Access.Infrastructure;

using System.Security.Cryptography;
using System.Text;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;

/// <summary>
/// AES-256-GCM com nonce aleatório para guardar o documento, o nome e o telefone, e HMAC-SHA256 com outra chave
/// para a unicidade do documento.
/// Formato cifrado: <c>v1.base64(nonce + tag + texto)</c>; a versão indica a chave usada, então uma chave
/// anterior continua lendo o que gravou enquanto a rotação não termina.
/// O tipo entra no HMAC e nos dados autenticados, então um valor não serve para outro tipo.
/// </summary>
public sealed class AesGcmDocumentProtector : IDocumentProtector
{
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly DocumentProtectionKeys _keys;

    public AesGcmDocumentProtector(DocumentProtectionKeys keys) => _keys = keys;

    public string CurrentKeyVersion => _keys.CurrentVersion;

    public ProtectedDocument Protect(Document document) =>
        new(document.Type, Encrypt(document.Value, Aad(document.Type)), ComputeHmac(document));

    public Document Reveal(ProtectedDocument document) =>
        Document.Create(document.Type, Decrypt(document.Encrypted, Aad(document.Type)));

    public ProtectedDocument RotateDocument(ProtectedDocument document) =>
        document with { Encrypted = Encrypt(Reveal(document).Value, Aad(document.Type)) };

    public string ProtectField(ClientField field, string value) => Encrypt(value, Aad(field));

    public string RevealField(ClientField field, string encrypted) => Decrypt(encrypted, Aad(field));

    private string Encrypt(string value, byte[] aad)
    {
        var plain = Encoding.UTF8.GetBytes(value);
        var payload = new byte[NonceBytes + TagBytes + plain.Length];
        var nonce = payload.AsSpan(0, NonceBytes);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_keys.Encryption, TagBytes);
        aes.Encrypt(nonce, plain, payload.AsSpan(NonceBytes + TagBytes), payload.AsSpan(NonceBytes, TagBytes), aad);

        return $"{CurrentKeyVersion}.{Convert.ToBase64String(payload)}";
    }

    private string Decrypt(string encrypted, byte[] aad)
    {
        var separator = encrypted.IndexOf('.');
        if (separator < 0 || !DocumentProtectionKeys.IsVersion(encrypted[..separator]))
            throw new InvalidOperationException("The value was protected with an unknown key version.");

        var version = encrypted[..separator];
        var key = _keys.ForVersion(version)
            ?? throw new InvalidOperationException(
                $"The value was protected with key version {version}, which is not configured. Add it to {DocumentProtectionKeys.PreviousKeysName}.");

        try
        {
            var payload = Convert.FromBase64String(encrypted[(separator + 1)..]);
            var plain = new byte[payload.Length - NonceBytes - TagBytes];

            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(payload.AsSpan(0, NonceBytes), payload.AsSpan(NonceBytes + TagBytes), payload.AsSpan(NonceBytes, TagBytes), plain, aad);

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The value could not be decrypted. Check {DocumentProtectionKeys.EncryptionKeyName}.");
        }
    }

    private string ComputeHmac(Document document)
    {
        var hash = HMACSHA256.HashData(_keys.Hmac, Encoding.UTF8.GetBytes($"{document.Type}:{document.Value}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] Aad(ClientType type) => Encoding.UTF8.GetBytes($"neoparking.client.document:{type}");

    private static byte[] Aad(ClientField field) => Encoding.UTF8.GetBytes($"neoparking.client.field:{field}");
}
