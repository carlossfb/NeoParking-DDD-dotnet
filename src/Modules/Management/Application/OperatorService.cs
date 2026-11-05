namespace NeoParking.Management.Application;

using NeoParking.Management.Domain;
using NeoParking.Management.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Primitives;

public sealed class OperatorService
{
    private readonly IOperatorRepository _repo;
    private readonly IPasswordHasher     _hasher;
    private readonly IUnitOfWork         _unitOfWork;

    public OperatorService(IOperatorRepository repo, IPasswordHasher hasher, IUnitOfWork unitOfWork)
    {
        _repo   = repo;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
    }

    // ── autenticação ──────────────────────────────────────────────────────────

    /// <summary>
    /// Valida credenciais e retorna os dados públicos do operador. E-mail inexistente, senha errada e conta
    /// inativa produzem a mesma exceção, para não revelar o estado da conta.
    /// Gerar o token JWT é responsabilidade da camada de API (infraestrutura de transporte).
    /// </summary>
    public async Task<OperatorResponseDTO> LoginAsync(LoginRequest request)
    {
        var user = await _repo.GetByEmailAsync(request.Email)
            ?? throw new InvalidCredentialsException();

        if (!_hasher.Verify(request.Password, user.PasswordHash.Value) || !user.IsActive)
            throw new InvalidCredentialsException();

        return ToResponse(user);
    }

    // ── criar owner ──────────────────────────────────────────────────────────

    /// <summary>
    /// Cria o único Owner do sistema. Falha se já existir um ou se o e-mail estiver em uso.
    /// </summary>
    public async Task<OperatorResponseDTO> CreateOwnerAsync(CreateOwnerRequest request)
    {
        if (await _repo.ExistsOwnerAsync())
            throw new ConflictException("Owner already exists. Only one Owner is allowed.");

        await EnsureEmailAvailableAsync(request.Email);

        var hash = PasswordHash.FromHash(_hasher.Hash(request.Password));
        var owner = OperatorUser.Create(request.Name, request.Email, hash, OperatorRole.Owner);

        await _repo.AddAsync(owner);
        await _unitOfWork.SaveChangesAsync();
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
        var requester = await GetActiveRequesterAsync(requesterId);

        if (requester.Role == OperatorRole.Operator)
            throw new ForbiddenException("Insufficient permissions to create operators.");

        var targetRole = ParseRole(request.Role);

        if (targetRole != OperatorRole.Operator && requester.Role != OperatorRole.Owner)
            throw new ForbiddenException("Only the Owner can create Admin users.");

        if (targetRole == OperatorRole.Owner)
            throw new DomainException("Cannot create additional Owner accounts.");

        await EnsureEmailAvailableAsync(request.Email);

        var hash = PasswordHash.FromHash(_hasher.Hash(request.Password));
        var user = OperatorUser.Create(request.Name, request.Email, hash, targetRole);

        await _repo.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return ToResponse(user);
    }

    // ── promoção / rebaixamento ───────────────────────────────────────────────

    public async Task PromoteToAdminAsync(Guid requesterId, Guid targetId)
    {
        var requester = await GetActiveRequesterAsync(requesterId);

        if (requester.Role != OperatorRole.Owner)
            throw new ForbiddenException("Only the Owner can promote operators to Admin.");

        var target = await GetExistingAsync(targetId);
        target.PromoteToAdmin();
        await _repo.UpdateAsync(target);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DemoteToOperatorAsync(Guid requesterId, Guid targetId)
    {
        var requester = await GetActiveRequesterAsync(requesterId);

        if (requester.Role != OperatorRole.Owner)
            throw new ForbiddenException("Only the Owner can demote Admin users.");

        var target = await GetExistingAsync(targetId);
        target.DemoteToOperator();
        await _repo.UpdateAsync(target);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── ativação / desativação ────────────────────────────────────────────────

    public async Task DeactivateAsync(Guid requesterId, Guid targetId)
    {
        var requester = await GetActiveRequesterAsync(requesterId);

        if (requester.Role == OperatorRole.Operator)
            throw new ForbiddenException("Insufficient permissions to deactivate operators.");

        var target = await GetExistingAsync(targetId);

        // Admin não pode desativar outro Admin ou Owner
        if (requester.Role == OperatorRole.Admin && target.Role >= OperatorRole.Admin)
            throw new ForbiddenException("Cannot deactivate a user with the same or higher role.");

        target.Deactivate();
        await _repo.UpdateAsync(target);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ActivateAsync(Guid requesterId, Guid targetId)
    {
        var requester = await GetActiveRequesterAsync(requesterId);

        if (requester.Role == OperatorRole.Operator)
            throw new ForbiddenException("Insufficient permissions to activate operators.");

        var target = await GetExistingAsync(targetId);

        // Admin não pode ativar outro Admin ou Owner
        if (requester.Role == OperatorRole.Admin && target.Role >= OperatorRole.Admin)
            throw new ForbiddenException("Cannot activate a user with the same or higher role.");

        target.Activate();
        await _repo.UpdateAsync(target);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── consulta ─────────────────────────────────────────────────────────────

    public async Task<OperatorResponseDTO> GetByIdAsync(Guid requesterId, Guid id)
    {
        var requester = await GetActiveRequesterAsync(requesterId);

        if (requester.Role == OperatorRole.Operator)
            throw new ForbiddenException("Insufficient permissions to read operators.");

        var user = await _repo.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(OperatorUser), id);

        return ToResponse(user);
    }

    public async Task<PagedResult<OperatorResponseDTO>> ListAsync(Guid requesterId, PageRequest page)
    {
        var requester = await GetActiveRequesterAsync(requesterId);

        if (requester.Role == OperatorRole.Operator)
            throw new ForbiddenException("Insufficient permissions to read operators.");

        var result = await _repo.ListAsync(page);

        return new PagedResult<OperatorResponseDTO>(
            result.Items.Select(ToResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Papel e estado do solicitante vêm do banco, não do token: um token ainda válido
    /// não basta depois de uma desativação ou de um rebaixamento (decisão D3).
    /// </summary>
    private async Task<OperatorUser> GetActiveRequesterAsync(Guid id)
    {
        var requester = await _repo.GetByIdAsync(id);

        if (requester is null || !requester.IsActive)
            throw new ForbiddenException("The requester is not allowed to perform this action.");

        return requester;
    }

    private async Task<OperatorUser> GetExistingAsync(Guid id)
        => await _repo.GetByIdAsync(id)
           ?? throw new NotFoundException(nameof(OperatorUser), id);

    private async Task EnsureEmailAvailableAsync(string email)
    {
        if (await _repo.GetByEmailAsync(email) is not null)
            throw new ConflictException($"E-mail '{email}' is already in use.");
    }

    private static OperatorRole ParseRole(string role)
        => Enum.TryParse<OperatorRole>(role, ignoreCase: true, out var parsed)
            ? parsed
            : throw new DomainException($"Invalid role '{role}'.");

    private static OperatorResponseDTO ToResponse(OperatorUser u)
        => new(u.Id, u.Name, u.Email, u.Role.ToString(), u.IsActive);
}
