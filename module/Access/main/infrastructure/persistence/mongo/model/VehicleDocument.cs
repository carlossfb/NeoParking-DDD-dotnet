using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace main.infrastructure.persistence.mongo.model
{
    public class VehicleDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid Id { get; set; }
        
        public string? Plate { get; set; }
        
        // Sem ClientId - não precisa no MongoDB pois está embedded
    }
}