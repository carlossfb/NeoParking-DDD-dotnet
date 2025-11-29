using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using src.domain.entity;

namespace src.domain.ports
{
    public interface IClientRepository
    {
        Task AddAsync(Client client);                // Persistir novo cliente
        Task UpdateAsync(Client client);             // Atualizar cliente existente
        Task<Client?> GetByIdAsync(Guid clientId);   // Obter cliente por Id
        Task<IEnumerable<Client>> GetAllAsync();    // Listar todos os clientes
        Task DeleteAsync(Client client);            // Remover cliente
    }
}