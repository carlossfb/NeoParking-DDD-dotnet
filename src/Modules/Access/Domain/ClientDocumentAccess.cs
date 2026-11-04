namespace NeoParking.Access.Domain;

using NeoParking.Shared.Kernel.Primitives;

/// <summary>Quem leu o documento completo de um cliente, e quando. Nunca guarda o valor do documento.</summary>
public sealed class ClientDocumentAccess : Entity
{
    public Guid ClientId { get; private set; }
    public Guid OperatorId { get; private set; }
    public DateTime AccessedAt { get; private set; }

    private ClientDocumentAccess() : base(Guid.Empty) { }

    private ClientDocumentAccess(Guid clientId, Guid operatorId) : base()
    {
        ClientId = clientId;
        OperatorId = operatorId;
        AccessedAt = DateTime.UtcNow;
    }

    public static ClientDocumentAccess Record(Guid clientId, Guid operatorId) => new(clientId, operatorId);
}
