namespace NeoParking.Access.Tests;

using NeoParking.Access.Domain;

public static class TestDocuments
{
    public static Document Cpf(string value) => Document.Create(ClientType.Individual, value);
}
