using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NeoParking.Access.Infrastructure;

    [BsonIgnoreExtraElements]
    public class ClientDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        
        public string? Name { get; set; }
        
        public string? PhoneNumber { get; set; }
        
        public string? Cpf { get; set; }
        
        public List<VehicleDocument> Vehicles { get; set; } = [];
    }
