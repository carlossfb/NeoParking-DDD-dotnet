using main.domain.entity;
using main.domain.vo;
using main.infrastructure.persistence.mongo.model;

namespace main.infrastructure.persistence.mongo.util
{
    internal static class ClientDocumentMapper
    {
        public static ClientDocument DomainToDocument(Client client)
        {
            return new ClientDocument
            {
                Id = client.Id,
                Name = client.Name,
                PhoneNumber = client.PhoneNumber.Value,
                Cpf = client.Cpf.Document,
                Vehicles = client.Vehicles
                    .Select(v => new VehicleDocument
                    {
                        Id = v.Id,
                        Plate = v.Plate.Document
                    })
                    .ToList()
            };
        }

        public static Client DocumentToDomain(ClientDocument document)
        {
            var client = Client.Create(
                document.Name ?? string.Empty,
                PhoneNumber.Create(document.PhoneNumber ?? string.Empty),
                Cpf.Create(document.Cpf ?? string.Empty)
            );

            // Setar o Id usando reflection
            var idProperty = client.GetType().GetProperty("Id");
            idProperty?.SetValue(client, document.Id);

            // Adicionar veículos
            foreach (var vehicleDoc in document.Vehicles)
            {
                var plate = Plate.Create(vehicleDoc.Plate ?? string.Empty);
                client.RegisterVehicle(plate, vehicleDoc.Id);
            }

            return client;
        }
    }
}