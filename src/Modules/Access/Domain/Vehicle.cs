namespace NeoParking.Access.Domain;

using NeoParking.Shared.Kernel.Primitives;

public sealed class Vehicle : Entity
{
    public Plate Plate { get; private set; }
    public Guid ClientId { get; private set; }
    private Vehicle() : base(Guid.Empty)
    {
        Plate = null!;
    }
    private Vehicle(Plate plate, Guid clientId) : base()
    {
        Plate = plate;
        ClientId = clientId;
    }

    private Vehicle(Guid id, Plate plate, Guid clientId) : base(id)
    {
        Plate = plate;
        ClientId = clientId;
    }

    internal static Vehicle Create(Plate plate, Guid clientId) =>
        new(plate, clientId);

    internal static Vehicle Reconstitute(Guid id, Plate plate, Guid clientId) =>
        new(id, plate, clientId);
}