namespace src.infrastructure.dao
{
    public class ClientDAO
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Cpf { get; set; }
        public List<VehicleDAO>? Vehicles { get; set; }       
    }
}