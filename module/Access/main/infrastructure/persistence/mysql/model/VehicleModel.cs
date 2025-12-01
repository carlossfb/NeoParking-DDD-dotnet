namespace main.infrastructure.persistence.mysql.model
{
    public class VehicleModel
    {
        public Guid Id {get; set;}
        public string? Plate {get; set;}
        public Guid ClientId { get; set; }
        
        // Navigation property
        public ClientModel? Client { get; set; }
    }
}