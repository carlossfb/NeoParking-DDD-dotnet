using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using src.domain.entity;
using src.domain.vo;

namespace src.application.dto
{
    public sealed record ClientResponseDTO(Guid Id, string Name, PhoneNumber PhoneNumber, Cpf Cpf, List<VehicleResponseDTO> Vehicles);
}