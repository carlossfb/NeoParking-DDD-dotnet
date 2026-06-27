namespace NeoParking.Access.Infrastructure;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class VehicleDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }
    public string? Plate { get; set; }
}