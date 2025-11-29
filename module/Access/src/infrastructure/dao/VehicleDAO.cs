namespace src.infrastructure.dao
{
    public class VehicleDAO
    {
        public Guid Id {get; set;}
        public string? Plate {get; set;}
        public Guid ClientId { get; set; }
        
        // Navigation property
        public ClientDAO? Client { get; set; }
    }
}