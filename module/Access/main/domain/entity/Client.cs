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


        private Client(string name, PhoneNumber phoneNumber, Cpf cpf)
        {

            if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Name is required");
            if (name.Length < 3) throw new DomainException("Name is too short");

            Id = Guid.NewGuid();
            Name = name;
            PhoneNumber = phoneNumber;
            Cpf = cpf;
            Vehicles = new List<Vehicle>();
        }

        public static Client Create(string name, PhoneNumber phoneNumber, Cpf cpf) => new Client(name, phoneNumber, cpf);
        
        public Vehicle RegisterVehicle(Plate plate)
        {
            if (plate == null)
                throw new DomainException("Plate is required");

            var vehicle = new Vehicle(plate);
            Vehicles.Add(vehicle);
            return vehicle;
        }
    }
}