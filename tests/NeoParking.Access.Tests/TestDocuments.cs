namespace NeoParking.Access.Tests;

using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using NeoParking.Access.Application;
using NeoParking.Access.Domain;
using NeoParking.Access.Infrastructure;

/// <summary>O protetor real, com chaves geradas a cada execução: nenhuma fica versionada.</summary>
public static class TestDocuments
{
    public static IDocumentProtector Protector { get; } = new AesGcmDocumentProtector(
        DocumentProtectionKeys.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DocumentProtectionKeys.EncryptionKeyName] = RandomNumberGenerator.GetHexString(64),
                [DocumentProtectionKeys.HmacKeyName] = RandomNumberGenerator.GetHexString(64),
            })
            .Build()));

    public static ProtectedDocument Cpf(string value) =>
        Protector.Protect(Document.Create(ClientType.Individual, value));
}
