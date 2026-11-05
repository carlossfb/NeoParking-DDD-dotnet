namespace NeoParking.Access.Infrastructure;

using System.Security.Cryptography;
using System.Text;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;

/// <summary>
/// AES-256-GCM com nonce aleatório para guardar o documento, e HMAC-SHA256 com outra chave para a unicidade.
/// Formato cifrado: <c>v1.base64(nonce + tag + texto)</c>; a versão indica a chave usada.
/// O tipo entra no HMAC e nos dados autenticados, então um valor não serve para outro tipo.
/// </summary>
public sealed class AesGcmDocumentProtector : IDocumentProtector
{
    private const string CurrentKeyVersion = "v1";
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly DocumentProtectionKeys _keys;

    public AesGcmDocumentProtector(DocumentProtectionKeys keys) => _keys = keys;

    public ProtectedDocument Protect(Document document) =>
        new(document.Type, Encrypt(document), ComputeHmac(document));

    public Document Reveal(ProtectedDocument document) =>
        Document.Create(document.Type, Decrypt(document));

    private string Encrypt(Document document)
    {
        var plain = Encoding.UTF8.GetBytes(document.Value);
        var payload = new byte[NonceBytes + TagBytes + plain.Length];
        var nonce = payload.AsSpan(0, NonceBytes);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_keys.Encryption, TagBytes);
        aes.Encrypt(nonce, plain, payload.AsSpan(NonceBytes + TagBytes), payload.AsSpan(NonceBytes, TagBytes), Aad(document.Type));

        return $"{CurrentKeyVersion}.{Convert.ToBase64String(payload)}";
    }

    private string Decrypt(ProtectedDocument document)
    {
        var separator = document.Encrypted.IndexOf('.');
        if (separator < 0 || document.Encrypted[..separator] != CurrentKeyVersion)
            throw new InvalidOperationException("The document was protected with an unknown key version.");

        try
        {
            var payload = Convert.FromBase64String(document.Encrypted[(separator + 1)..]);
            var plain = new byte[payload.Length - NonceBytes - TagBytes];

            using var aes = new AesGcm(_keys.Encryption, TagBytes);
            aes.Decrypt(payload.AsSpan(0, NonceBytes), payload.AsSpan(NonceBytes + TagBytes), payload.AsSpan(NonceBytes, TagBytes), plain, Aad(document.Type));

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The document could not be decrypted. Check {DocumentProtectionKeys.EncryptionKeyName}.");
        }
    }

    private string ComputeHmac(Document document)
    {
        var hash = HMACSHA256.HashData(_keys.Hmac, Encoding.UTF8.GetBytes($"{document.Type}:{document.Value}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] Aad(ClientType type) => Encoding.UTF8.GetBytes($"neoparking.client.document:{type}");
}
