using MongoDB.Driver;
using main.domain.entity;
using main.domain.ports;
using main.infrastructure.persistence.mongo.model;
using main.infrastructure.persistence.mongo.util;

namespace main.infrastructure.persistence.mongo
{
    public class MongoRepositoryImpl : IClientRepository
    {
        private readonly IMongoCollection<ClientDocument> _clients;

        public MongoRepositoryImpl(MongoDbContext context)
        {
            _clients = context.Clients;
        }

        public async Task AddAsync(Client client)
        {
            var document = ClientDocumentMapper.DomainToDocument(client);
            await _clients.InsertOneAsync(document);
        }

        public async Task DeleteAsync(Client client)
        {
            await _clients.DeleteOneAsync(c => c.Id == client.Id);
        }

        public async Task<IEnumerable<Client>> GetAllAsync()
        {
            var documents = await _clients.Find(_ => true).ToListAsync();
            return documents.Select(ClientDocumentMapper.DocumentToDomain);
        }

        public async Task<Client?> GetByIdAsync(Guid clientId)
        {
            var document = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
            return document == null ? null : ClientDocumentMapper.DocumentToDomain(document);
        }

        public async Task UpdateAsync(Client client)
        {
            var document = ClientDocumentMapper.DomainToDocument(client);
            await _clients.ReplaceOneAsync(c => c.Id == client.Id, document);
        }
    }
}