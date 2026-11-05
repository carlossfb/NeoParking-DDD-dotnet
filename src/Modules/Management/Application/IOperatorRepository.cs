namespace NeoParking.Management.Application;

using NeoParking.Management.Domain;
using NeoParking.Shared.Kernel.Primitives;

public interface IOperatorRepository
{
    Task AddAsync(OperatorUser user);
    Task<OperatorUser?> GetByIdAsync(Guid id);
    Task<OperatorUser?> GetByEmailAsync(string email);
    Task<PagedResult<OperatorUser>> ListAsync(PageRequest page);
    Task UpdateAsync(OperatorUser user);
    Task<bool> ExistsOwnerAsync();
}
