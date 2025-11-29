using src.domain.exception;
using src.domain.vo;

namespace src.domain.entity
{
    public class Vehicle
    {
        public Guid Id {get; private set;}
        public Plate Plate {get; private set;}

        internal Vehicle(Plate plate)
        {
            Id = Guid.NewGuid();
            Plate = plate ?? throw new DomainException("Plate is required");
        }
    }
}