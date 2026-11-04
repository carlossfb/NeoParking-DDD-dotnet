namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;

public interface IDocumentProtector
{
    ProtectedDocument Protect(Document document);

    Document Reveal(ProtectedDocument document);
}
