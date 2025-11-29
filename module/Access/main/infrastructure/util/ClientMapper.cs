    using System.Linq;
    using main.application.dto;
    using main.domain.entity;
    using main.domain.vo;
    using main.infrastructure.dao;

    namespace main.infrastructure.util
    {
        internal static class ClientMapper
        {
            // Domínio → DAO
            public static ClientDAO DomainToDAO(Client client)
            {
                return new ClientDAO
                {
                    Id = client.Id,
                    Name = client.Name,
                    PhoneNumber = client.PhoneNumber.ToString(),
                    Cpf = client.Cpf.ToString(),
                    Vehicles = client.Vehicles
                        .Select(v => new VehicleDAO
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
                var client = Client.Create(
                    dto.Name,
                    PhoneNumber.Create(dto.PhoneNumber.ToString()),
                    Cpf.Create(dto.Cpf.ToString())
                );

                return client;
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

            public static Client DAOToDomain(ClientDAO dao)
            {
                // Criar cliente com dados básicos
                var client = Client.Create(
                    dao.Name ?? string.Empty,
                    PhoneNumber.Create(dao.PhoneNumber ?? string.Empty),
                    Cpf.Create(dao.Cpf ?? string.Empty)
                );

                // Setar o Id que veio do banco (usando reflection)
                var idProperty = client.GetType().GetProperty("Id");
                idProperty?.SetValue(client, dao.Id);

                // Adicionar veículos
                if (dao.Vehicles != null)
                {
                    foreach (var vehicleDao in dao.Vehicles)
                    {
                        var plate = Plate.Create(vehicleDao.Plate ?? string.Empty);
                        client.RegisterVehicle(plate);
                    }
                }

                return client;
            }

        }
    }
