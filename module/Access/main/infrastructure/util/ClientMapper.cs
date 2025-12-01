    using System.Linq;
    using main.application.dto;
    using main.domain.entity;
    using main.domain.vo;
    using main.infrastructure.persistence.mysql.model;

    namespace main.infrastructure.util
    {
        internal static class ClientMapper
        {
            // Domínio → model
            public static ClientModel DomainToModel(Client client)
            {
                return new ClientModel
                {
                    Id = client.Id,
                    Name = client.Name,
                    PhoneNumber = client.PhoneNumber.ToString(),
                    Cpf = client.Cpf.ToString(),
                    Vehicles = client.Vehicles
                        .Select(v => new VehicleModel
                        {
                            Id = v.Id,
                            Plate = v.Plate.Document,
                            ClientId = client.Id // FK para EF
                        })
                        .ToList()
                };
            }

        public static Client RequestDTOToDomain(ClientRequestDTO dto)
        {
            var vehicles = dto.Vehicles?.Select(vDto => 
                new Vehicle(Plate.Create(vDto.Plate), Guid.Empty)
            ).ToList() ?? new List<Vehicle>();

            return Client.Create(
                dto.Name,
                PhoneNumber.Create(dto.PhoneNumber.ToString()),
                Cpf.Create(dto.Cpf.ToString()),
                vehicles
            );
        }


            public static ClientResponseDTO DomainToResponseDTO(Client client)
            {
                return new ClientResponseDTO(
                    client.Id,
                    client.Name,
                    client.PhoneNumber,
                    client.Cpf,
                    client.Vehicles.Select(v => new VehicleResponseDTO(v.Id, v.Plate)).ToList()
                );
            }

            public static Client ModelToDomain(ClientModel model)
            {
                // Criar cliente com dados básicos
                var client = Client.Create(
                    model.Name ?? string.Empty,
                    PhoneNumber.Create(model.PhoneNumber ?? string.Empty),
                    Cpf.Create(model.Cpf ?? string.Empty)
                );

                // Setar o Id que veio do banco (usando reflection)
                var idProperty = client.GetType().GetProperty("Id");
                idProperty?.SetValue(client, model.Id);

                // Adicionar veículos
                if (model.Vehicles != null)
                {
                    foreach (var VehicleModel in model.Vehicles)
                    {
                        var plate = Plate.Create(VehicleModel.Plate ?? string.Empty);
                        var clientId = VehicleModel.ClientId;
                        client.RegisterVehicle(plate, clientId);
                    }
                }

                return client;
            }

            public static Client UpdateClientFromDTO(Client existingClient, ClientUpdateDTO dto)
            {
                // Cria um novo cliente com os dados atualizados (vehicles sempre preservados)
                var updatedClient = Client.Create(
                    dto.Name ?? existingClient.Name,
                    dto.PhoneNumber != null ? PhoneNumber.Create(dto.PhoneNumber) : existingClient.PhoneNumber,
                    dto.Cpf != null ? Cpf.Create(dto.Cpf) : existingClient.Cpf,
                    existingClient.Vehicles.ToList()
                );

                // Preserva o ID original
                var idProperty = updatedClient.GetType().GetProperty("Id");
                idProperty?.SetValue(updatedClient, existingClient.Id);

                return updatedClient;
            }

        }
    }
