namespace NeoParking.Management.Application;

using NeoParking.Management.Domain;

public interface IOperatorRepository
{
    Task AddAsync(OperatorUser user);
    Task<OperatorUser?> GetByIdAsync(Guid id);
    Task<OperatorUser?> GetByEmailAsync(string email);
    Task UpdateAsync(OperatorUser user);
    Task<bool> ExistsOwnerAsync();
}
