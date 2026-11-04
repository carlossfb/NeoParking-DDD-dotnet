namespace NeoParking.Access.Domain;

/// <summary>Como o documento fica guardado: cifrado, com um HMAC para garantir a unicidade sem decifrar.</summary>
public sealed record ProtectedDocument(ClientType Type, string Encrypted, string Hmac);
