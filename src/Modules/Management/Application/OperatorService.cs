namespace NeoParking.Management.Application;

using NeoParking.Management.Domain;
using NeoParking.Management.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;

public sealed class OperatorService
{
    private readonly IOperatorRepository _repo;
    private readonly IPasswordHasher     _hasher;

    public OperatorService(IOperatorRepository repo, IPasswordHasher hasher)
    {
        _repo   = repo;
        _hasher = hasher;
    }

    // ── autenticação ──────────────────────────────────────────────────────────

    /// <summary>
    /// Valida credenciais e retorna o operador. Lança DomainException se inválido ou inativo.
    /// Gerar o token JWT é responsabilidade da camada de API (infraestrutura de transporte).
    /// </summary>
    public async Task<OperatorUser> LoginAsync(LoginRequest request)
    {
        var user = await _repo.GetByEmailAsync(request.Email)
            ?? throw new DomainException("Invalid credentials.");

        if (!user.IsActive)
            throw new DomainException("Account is inactive.");

        if (!_hasher.Verify(request.Password, user.PasswordHash.Value))
            throw new DomainException("Invalid credentials.");

        return user;
    }

    // ── criar owner ──────────────────────────────────────────────────────────

    /// <summary>
    /// Cria o único Owner do sistema. Falha se já existir um ou se o e-mail estiver em uso.
    /// </summary>
    public async Task<OperatorResponseDTO> CreateOwnerAsync(CreateOwnerRequest request)
    {
        if (await _repo.ExistsOwnerAsync())
            throw new DomainException("Owner already exists. Only one Owner is allowed.");

        await EnsureEmailAvailableAsync(request.Email);

        var hash = PasswordHash.FromHash(_hasher.Hash(request.Password));
        var owner = OperatorUser.Create(request.Name, request.Email, hash, OperatorRole.Owner);

        await _repo.AddAsync(owner);
        return ToResponse(owner);
    }

    // ── criar operator / admin ───────────────────────────────────────────────

    /// <summary>
    /// Owner pode criar Operator ou Admin.
    /// Admin pode criar apenas Operator.
    /// Operator não pode criar ninguém.
    /// </summary>
    public async Task<OperatorResponseDTO> CreateOperatorAsync(Guid requesterId, CreateOperatorRequest request)
    {
        var requester = await GetExistingAsync(requesterId);

        if (requester.Role == OperatorRole.Operator)
            throw new DomainException("Insufficient permissions to create operators.");

        var targetRole = ParseRole(request.Role);

        if (targetRole == OperatorRole.Admin && requester.Role != OperatorRole.Owner)
            throw new DomainException("Only the Owner can create Admin users.");

        if (targetRole == OperatorRole.Owner)
            throw new DomainException("Cannot create additional Owner accounts.");

        await EnsureEmailAvailableAsync(request.Email);

        var hash = PasswordHash.FromHash(_hasher.Hash(request.Password));
        var user = OperatorUser.Create(request.Name, request.Email, hash, targetRole);

        await _repo.AddAsync(user);
        return ToResponse(user);
    }

    // ── promoção / rebaixamento ───────────────────────────────────────────────

    public async Task PromoteToAdminAsync(Guid requesterId, Guid targetId)
    {
        var requester = await GetExistingAsync(requesterId);

        if (requester.Role != OperatorRole.Owner)
            throw new DomainException("Only the Owner can promote operators to Admin.");

        var target = await GetExistingAsync(targetId);
        target.PromoteToAdmin();
        await _repo.UpdateAsync(target);
    }

    public async Task DemoteToOperatorAsync(Guid requesterId, Guid targetId)
    {
        var requester = await GetExistingAsync(requesterId);

        if (requester.Role != OperatorRole.Owner)
            throw new DomainException("Only the Owner can demote Admin users.");

        var target = await GetExistingAsync(targetId);
        target.DemoteToOperator();
        await _repo.UpdateAsync(target);
    }

    // ── ativação / desativação ────────────────────────────────────────────────

    public async Task DeactivateAsync(Guid requesterId, Guid targetId)
    {
        var requester = await GetExistingAsync(requesterId);
        var target    = await GetExistingAsync(targetId);

        if (requester.Role == OperatorRole.Operator)
            throw new DomainException("Insufficient permissions to deactivate operators.");

        // Admin não pode desativar outro Admin ou Owner
        if (requester.Role == OperatorRole.Admin && target.Role >= OperatorRole.Admin)
            throw new DomainException("Cannot deactivate a user with the same or higher role.");

        target.Deactivate();
        await _repo.UpdateAsync(target);
    }

    public async Task ActivateAsync(Guid requesterId, Guid targetId)
    {
        var requester = await GetExistingAsync(requesterId);

        if (requester.Role == OperatorRole.Operator)
            throw new DomainException("Insufficient permissions to activate operators.");

        var target = await GetExistingAsync(targetId);
        target.Activate();
        await _repo.UpdateAsync(target);
    }

    // ── consulta ─────────────────────────────────────────────────────────────

    public async Task<OperatorResponseDTO> GetByIdAsync(Guid id)
    {
        var user = await _repo.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(OperatorUser), id);

        return ToResponse(user);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<OperatorUser> GetExistingAsync(Guid id)
        => await _repo.GetByIdAsync(id)
           ?? throw new NotFoundException(nameof(OperatorUser), id);

    private async Task EnsureEmailAvailableAsync(string email)
    {
        if (await _repo.GetByEmailAsync(email) is not null)
            throw new DomainException($"E-mail '{email}' is already in use.");
    }

    private static OperatorRole ParseRole(string role)
        => Enum.TryParse<OperatorRole>(role, ignoreCase: true, out var parsed)
            ? parsed
            : throw new DomainException($"Invalid role '{role}'.");

    private static OperatorResponseDTO ToResponse(OperatorUser u)
        => new(u.Id, u.Name, u.Email, u.Role.ToString(), u.IsActive);
}
