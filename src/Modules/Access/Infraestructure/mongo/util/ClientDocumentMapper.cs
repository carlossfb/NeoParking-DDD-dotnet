namespace NeoParking.Access.Infrastructure;

using NeoParking.Access.Domain;

internal static class ClientDocumentMapper
{
    public static ClientDocument DomainToDocument(Client client) =>
        new()
        {
            Id = client.Id,
            Name = client.Name,
            PhoneNumber = client.PhoneNumber.Value,
            Cpf = client.Cpf.Document,
            Vehicles = client.Vehicles
                .Select(v => new VehicleDocument
                {
                    Id = v.Id,
                    Plate = v.Plate.Value
                })
                .ToList()
        };

    public static Client DocumentToDomain(ClientDocument doc)
    {
        var vehicles = doc.Vehicles
            .Select(v => Vehicle.Reconstitute(
                v.Id,
                Plate.Create(v.Plate ?? string.Empty),
                doc.Id))
            .ToList();

        return Client.Reconstitute(
            doc.Id,
            doc.Name ?? string.Empty,
            PhoneNumber.Create(doc.PhoneNumber ?? string.Empty),
            Cpf.Create(doc.Cpf ?? string.Empty),
            vehicles);
    }
}