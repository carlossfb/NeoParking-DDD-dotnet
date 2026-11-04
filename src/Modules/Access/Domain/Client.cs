namespace NeoParking.Access.Domain;

using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Primitives;

public sealed class Client : Entity
{
    public string Name { get; private set; }
    public PhoneNumber PhoneNumber { get; private set; }
    public ClientType Type { get; private set; }
    public string DocumentValue { get; private set; }
    public Document Document => Document.Create(Type, DocumentValue);

    private readonly List<Vehicle> _vehicles = [];
    public IReadOnlyList<Vehicle> Vehicles => _vehicles.AsReadOnly();

    // Construtor para o EF Core — private, sem parâmetros
    private Client() : base(Guid.Empty)
    {
        Name = string.Empty;
        PhoneNumber = null!;
        DocumentValue = string.Empty;
    }


    private Client(string name, PhoneNumber phoneNumber, Document document) : base()
    {
        ValidateName(name);
        Name = name;
        PhoneNumber = phoneNumber;
        Type = document.Type;
        DocumentValue = document.Value;
    }

    private Client(Guid id, string name, PhoneNumber phoneNumber, Document document, IEnumerable<Vehicle> vehicles) : base(id)
    {
        ValidateName(name);
        Name = name;
        PhoneNumber = phoneNumber;
        Type = document.Type;
        DocumentValue = document.Value;
        _vehicles = vehicles.ToList();
    }

    public static Client Create(string name, PhoneNumber phoneNumber, Document document) =>
        new(name, phoneNumber, document);

    public static Client Reconstitute(Guid id, string name, PhoneNumber phoneNumber, Document document, IEnumerable<Vehicle> vehicles) =>
        new(id, name, phoneNumber, document, vehicles);

    public Vehicle RegisterVehicle(Plate plate)
    {
        if (plate is null)
            throw new DomainException("Plate is required");

        if (_vehicles.Any(v => v.Plate.Value == plate.Value))
            throw new DomainException("This plate is already registered for this client.");

        var vehicle = Vehicle.Create(plate, Id);
        _vehicles.Add(vehicle);
        return vehicle;
    }

    public void Update(string? name, string? phoneNumber, Document? document)
    {
        if (name is not null)
        {
            ValidateName(name);
            Name = name;
        }
        if (phoneNumber is not null) PhoneNumber = PhoneNumber.Create(phoneNumber);
        if (document is not null)
        {
            Type = document.Type;
            DocumentValue = document.Value;
        }
    }
    public void RemoveVehicle(Guid vehicleId)
    {
        var vehicle = _vehicles.FirstOrDefault(v => v.Id == vehicleId)
            ?? throw new DomainException("Vehicle not found");

        _vehicles.Remove(vehicle);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required");
        if (name.Length < 3) throw new DomainException("Name is too short");
    }
}