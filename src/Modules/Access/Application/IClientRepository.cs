namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;

public interface IClientRepository
{
    Task AddAsync(Client client);
    Task UpdateAsync(Client client);
    Task<Client?> GetByIdAsync(Guid clientId);
    Task<IEnumerable<Client>> GetAllAsync();
    Task DeleteAsync(Client client);
    Task AddDocumentAccessAsync(ClientDocumentAccess access);
}