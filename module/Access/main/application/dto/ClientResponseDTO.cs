using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using main.domain.entity;
using main.domain.vo;

namespace main.application.dto
{
    public sealed record ClientResponseDTO(Guid Id, string Name, PhoneNumber PhoneNumber, Cpf Cpf, List<VehicleResponseDTO> Vehicles);
}