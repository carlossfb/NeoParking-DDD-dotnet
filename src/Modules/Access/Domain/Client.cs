namespace NeoParking.Access.Domain;

using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Primitives;

public sealed class Client : Entity
{
    public string Name { get; private set; }
    public PhoneNumber PhoneNumber { get; private set; }
    public Cpf Cpf { get; private set; }

    private readonly List<Vehicle> _vehicles = [];
    public IReadOnlyList<Vehicle> Vehicles => _vehicles.AsReadOnly();

    // Construtor para o EF Core — private, sem parâmetros
    private Client() : base(Guid.Empty)
    {
        Name = string.Empty;
        PhoneNumber = null!;
        Cpf = null!;
    }


    private Client(string name, PhoneNumber phoneNumber, Cpf cpf) : base()
    {
        ValidateName(name);
        Name = name;
        PhoneNumber = phoneNumber;
        Cpf = cpf;
    }

    private Client(Guid id, string name, PhoneNumber phoneNumber, Cpf cpf, IEnumerable<Vehicle> vehicles) : base(id)
    {
        ValidateName(name);
        Name = name;
        PhoneNumber = phoneNumber;
        Cpf = cpf;
        _vehicles = vehicles.ToList();
    }

    public static Client Create(string name, PhoneNumber phoneNumber, Cpf cpf) =>
        new(name, phoneNumber, cpf);

    public static Client Reconstitute(Guid id, string name, PhoneNumber phoneNumber, Cpf cpf, IEnumerable<Vehicle> vehicles) =>
        new(id, name, phoneNumber, cpf, vehicles);

    public Vehicle RegisterVehicle(Plate plate)
    {
        if (plate is null)
            throw new DomainException("Plate is required");

        var vehicle = Vehicle.Create(plate, Id);
        _vehicles.Add(vehicle);
        return vehicle;
    }

    public void Update(string? name, string? phoneNumber, string? cpf)
    {
        if (name is not null)
        {
            ValidateName(name);
            Name = name;
        }
        if (phoneNumber is not null) PhoneNumber = PhoneNumber.Create(phoneNumber);
        if (cpf is not null) Cpf = Cpf.Create(cpf);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required");
        if (name.Length < 3) throw new DomainException("Name is too short");
    }
}