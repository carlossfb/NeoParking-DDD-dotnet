namespace NeoParking.Access.Application;

using NeoParking.Access.Domain;
using NeoParking.Shared.Kernel.Primitives;

public interface IClientRepository
{
    Task AddAsync(Client client);
    Task UpdateAsync(Client client);
    Task<Client?> GetByIdAsync(Guid clientId);
    Task<Client?> GetByPlateAsync(Plate plate);
    Task<Client?> GetByDocumentHmacAsync(string documentHmac);
    Task<PagedResult<Client>> ListAsync(PageRequest page);
    Task DeleteAsync(Client client);
    Task AddDocumentAccessAsync(ClientDocumentAccess access);
    Task<PagedResult<ClientDocumentAccess>> ListDocumentAccessesAsync(Guid clientId, PageRequest page);
}