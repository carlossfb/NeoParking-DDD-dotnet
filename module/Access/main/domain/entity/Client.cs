using main.domain.exception;
using main.domain.vo;

namespace main.domain.entity
{
    public class Client
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public PhoneNumber PhoneNumber { get; private set; }
        public Cpf Cpf { get; private set; }
        public List<Vehicle> Vehicles { get; private set; }


        private Client(string name, PhoneNumber phoneNumber, Cpf cpf, List<Vehicle>? vehicles)
        {

            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required");
            if (name.Length < 3) throw new DomainException("Name is too short");

            Id = Guid.NewGuid();
            Name = name;
            PhoneNumber = phoneNumber;
            Cpf = cpf;
            Vehicles = vehicles ?? new List<Vehicle>();
        }

        public static Client Create(string name, PhoneNumber phoneNumber, Cpf cpf, List<Vehicle> vehicles)
        {
            return new Client(name, phoneNumber, cpf, vehicles);
        }

        public static Client Create(string name, PhoneNumber phoneNumber, Cpf cpf)
        {
             return Create(name, phoneNumber, cpf, new List<Vehicle>());
        }
        
        
        public Vehicle RegisterVehicle(Plate plate, Guid clientId)
        {
            if (plate == null)
                throw new DomainException("Plate is required");

            var vehicle = new Vehicle(plate, clientId);
            Vehicles.Add(vehicle);
            return vehicle;
        }
    }
}