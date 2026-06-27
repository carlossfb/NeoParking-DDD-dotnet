namespace NeoParking.Access.Infrastructure;

using MongoDB.Driver;
using NeoParking.Access.Domain;

public sealed class MongoClientRepository : IClientRepository
{
    private readonly IMongoCollection<ClientDocument> _clients;

    public MongoClientRepository(MongoDbContext context) =>
        _clients = context.Clients;

    public async Task AddAsync(Client client) =>
        await _clients.InsertOneAsync(ClientDocumentMapper.DomainToDocument(client));

    public async Task DeleteAsync(Client client) =>
        await _clients.DeleteOneAsync(c => c.Id == client.Id);

    public async Task<IEnumerable<Client>> GetAllAsync()
    {
        var documents = await _clients.Find(_ => true).ToListAsync();
        return documents.Select(ClientDocumentMapper.DocumentToDomain);
    }

    public async Task<Client?> GetByIdAsync(Guid clientId)
    {
        var document = await _clients.Find(c => c.Id == clientId).FirstOrDefaultAsync();
        return document is null ? null : ClientDocumentMapper.DocumentToDomain(document);
    }

    public async Task UpdateAsync(Client client) =>
        await _clients.ReplaceOneAsync(c => c.Id == client.Id, ClientDocumentMapper.DomainToDocument(client));
}