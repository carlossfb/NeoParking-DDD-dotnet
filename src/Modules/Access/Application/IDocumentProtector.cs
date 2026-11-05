namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;

public interface IDocumentProtector
{
    /// <summary>A versão da chave usada nas novas gravações, como aparece no início do valor cifrado.</summary>
    string CurrentKeyVersion { get; }

    ProtectedDocument Protect(Document document);

    Document Reveal(ProtectedDocument document);

    /// <summary>Recifra com a chave atual. O HMAC continua o mesmo, porque a chave dele não roda.</summary>
    ProtectedDocument RotateDocument(ProtectedDocument document);

    string ProtectField(ClientField field, string value);

    string RevealField(ClientField field, string encrypted);
}
