namespace main.infrastructure.persistence.model
{
    public class ClientModel
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Cpf { get; set; }
        public List<VehicleModel>? Vehicles { get; set; }       
    }
}
