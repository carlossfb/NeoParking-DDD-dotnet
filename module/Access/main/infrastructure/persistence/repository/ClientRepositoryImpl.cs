using main.domain.entity;
using main.domain.ports;
using main.infrastructure.util;
using Microsoft.EntityFrameworkCore;

namespace main.infrastructure
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
            var model = ClientMapper.DomainToModel(client);
            await _context.Clients.AddAsync(model);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Client client)
        {
            var model = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == client.Id);

            if (model is null)
                return;

            _context.Clients.Remove(model);
            await _context.SaveChangesAsync();
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

            return dao is null ? null : ClientMapper.ModelToDomain(dao);
        }

        public Task UpdateAsync(Client client)
        {
            throw new NotImplementedException();
        }
    }
}