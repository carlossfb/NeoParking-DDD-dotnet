using src.domain.entity;
using src.domain.ports;
using src.infrastructure.util;
using Microsoft.EntityFrameworkCore;

namespace src.infrastructure
{
    public class ClientRepositoryImpl : IClientRepository 
    {
    
        private readonly AppDbContext _context;

        public ClientRepositoryImpl(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Client client)
        {
            var dao = ClientMapper.DomainToDAO(client); // Converte domínio → DAO
            await _context.Clients.AddAsync(dao);
            await _context.SaveChangesAsync();
        }

        public Task DeleteAsync(Client client)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Client>> GetAllAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<Client?> GetByIdAsync(Guid clientId)
        {
            var dao = await _context.Clients
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Id == clientId);

            return dao is null ? null : ClientMapper.DAOToDomain(dao);
        }

        public Task UpdateAsync(Client client)
        {
            throw new NotImplementedException();
        }
    }
}