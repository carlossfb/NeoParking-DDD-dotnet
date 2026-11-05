namespace NeoParking.Management.Tests.Unit.Application;

using FluentAssertions;
using Moq;
using NeoParking.Management.Application;
using NeoParking.Management.Domain;
using NeoParking.Management.Domain.Vo;
using NeoParking.Management.Infrastructure;
using NeoParking.Shared.Kernel.Exceptions;

public class OperatorServiceTests
{
    private readonly Mock<IOperatorRepository> _repo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly OperatorService _service;
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public OperatorServiceTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _service = new OperatorService(_repo.Object, _hasher.Object, _unitOfWork.Object);
    }

    private static OperatorUser MakeUser(OperatorRole role, Guid? id = null)
    {
        var user = OperatorUser.Create("User", "user@neo.com", PasswordHash.FromHash("hash"), role);
        // reconstitui com id específico se fornecido
        return id.HasValue
            ? OperatorUser.Reconstitute(id.Value, "User", "user@neo.com",
                PasswordHash.FromHash("hash"), role, true, DateTime.UtcNow)
            : user;
    }

    // ── criar owner ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOwnerAsync_WhenNoOwnerExists_ShouldSucceed()
    {
        _repo.Setup(r => r.ExistsOwnerAsync()).ReturnsAsync(false);
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((OperatorUser?)null);
        _repo.Setup(r => r.AddAsync(It.IsAny<OperatorUser>())).Returns(Task.CompletedTask);

        var result = await _service.CreateOwnerAsync(new CreateOwnerRequest("Owner", "owner@neo.com", "pass"));

        result.Role.Should().Be("Owner");
        _repo.Verify(r => r.AddAsync(It.IsAny<OperatorUser>()), Times.Once);
    }

    [Fact]
    public async Task CreateOwnerAsync_WhenOwnerAlreadyExists_ShouldThrowDomainException()
    {
        _repo.Setup(r => r.ExistsOwnerAsync()).ReturnsAsync(true);

        var act = async () => await _service.CreateOwnerAsync(new CreateOwnerRequest("Owner", "owner@neo.com", "pass"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Owner already exists*");
    }

    [Fact]
    public async Task CreateOwnerAsync_WithDuplicateEmail_ShouldThrowDomainException()
    {
        _repo.Setup(r => r.ExistsOwnerAsync()).ReturnsAsync(false);
        _repo.Setup(r => r.GetByEmailAsync("owner@neo.com"))
            .ReturnsAsync(MakeUser(OperatorRole.Operator));

        var act = async () => await _service.CreateOwnerAsync(new CreateOwnerRequest("Owner", "owner@neo.com", "pass"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*already in use*");
    }

    // ── criar operator ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOperatorAsync_ByOwner_ShouldSucceed()
    {
        var ownerId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(ownerId)).ReturnsAsync(MakeUser(OperatorRole.Owner, ownerId));
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((OperatorUser?)null);
        _repo.Setup(r => r.AddAsync(It.IsAny<OperatorUser>())).Returns(Task.CompletedTask);

        var result = await _service.CreateOperatorAsync(ownerId,
            new CreateOperatorRequest("Op", "op@neo.com", "pass", "Operator"));

        result.Role.Should().Be("Operator");
    }

    [Fact]
    public async Task CreateOperatorAsync_AdminCreatingAdmin_ShouldThrowForbiddenException()
    {
        var adminId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((OperatorUser?)null);

        var act = async () => await _service.CreateOperatorAsync(adminId,
            new CreateOperatorRequest("Op", "op@neo.com", "pass", "Admin"));

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Only the Owner*");
    }

    [Fact]
    public async Task CreateOperatorAsync_ByRegularOperator_ShouldThrowForbiddenException()
    {
        var opId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(opId)).ReturnsAsync(MakeUser(OperatorRole.Operator, opId));

        var act = async () => await _service.CreateOperatorAsync(opId,
            new CreateOperatorRequest("Op", "op@neo.com", "pass", "Operator"));

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Insufficient permissions*");
    }

    // ── promoção ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PromoteToAdminAsync_ByOwner_ShouldSucceed()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(ownerId)).ReturnsAsync(MakeUser(OperatorRole.Owner, ownerId));
        _repo.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(MakeUser(OperatorRole.Operator, targetId));
        _repo.Setup(r => r.UpdateAsync(It.IsAny<OperatorUser>())).Returns(Task.CompletedTask);

        await _service.PromoteToAdminAsync(ownerId, targetId);

        _repo.Verify(r => r.UpdateAsync(It.Is<OperatorUser>(u => u.Role == OperatorRole.Admin)), Times.Once);
    }

    [Fact]
    public async Task PromoteToAdminAsync_ByAdmin_ShouldThrowForbiddenException()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));
        _repo.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(MakeUser(OperatorRole.Operator, targetId));

        var act = async () => await _service.PromoteToAdminAsync(adminId, targetId);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Only the Owner*");
    }

    // ── desativação ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateAsync_AdminDeactivatingOperator_ShouldSucceed()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));
        _repo.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(MakeUser(OperatorRole.Operator, targetId));
        _repo.Setup(r => r.UpdateAsync(It.IsAny<OperatorUser>())).Returns(Task.CompletedTask);

        await _service.DeactivateAsync(adminId, targetId);

        _repo.Verify(r => r.UpdateAsync(It.Is<OperatorUser>(u => !u.IsActive)), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_OperatorDeactivatingAdmin_ShouldThrowForbiddenException()
    {
        var opId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(opId)).ReturnsAsync(MakeUser(OperatorRole.Operator, opId));
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));

        var act = async () => await _service.DeactivateAsync(opId, adminId);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Insufficient permissions*");
    }

    [Fact]
    public async Task DeactivateAsync_AdminDeactivatingAdmin_ShouldThrowForbiddenException()
    {
        var adminId = Guid.NewGuid();
        var targetAdminId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));
        _repo.Setup(r => r.GetByIdAsync(targetAdminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, targetAdminId));

        var act = async () => await _service.DeactivateAsync(adminId, targetAdminId);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*same or higher role*");
    }

    // ── not found ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ShouldThrowNotFoundException()
    {
        var adminId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((OperatorUser?)null);
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));

        var act = async () => await _service.GetByIdAsync(adminId, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── solicitante (TD-104) ─────────────────────────────────────────────────

    [Fact]
    public async Task Actions_ByInactiveRequester_ShouldThrowForbiddenException()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var inactive = OperatorUser.Reconstitute(requesterId, "User", "user@neo.com",
            PasswordHash.FromHash("hash"), OperatorRole.Owner, false, DateTime.UtcNow);
        _repo.Setup(r => r.GetByIdAsync(requesterId)).ReturnsAsync(inactive);
        _repo.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(MakeUser(OperatorRole.Operator, targetId));

        var actions = new Func<Task>[]
        {
            () => _service.CreateOperatorAsync(requesterId, new CreateOperatorRequest("Op", "op@neo.com", "pass", "Operator")),
            () => _service.GetByIdAsync(requesterId, targetId),
            () => _service.PromoteToAdminAsync(requesterId, targetId),
            () => _service.DemoteToOperatorAsync(requesterId, targetId),
            () => _service.DeactivateAsync(requesterId, targetId),
            () => _service.ActivateAsync(requesterId, targetId),
        };

        foreach (var action in actions)
            await action.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Actions_ByUnknownRequester_ShouldThrowForbiddenException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((OperatorUser?)null);

        var act = () => _service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ── ativação (TD-107) ────────────────────────────────────────────────────

    [Theory]
    [InlineData(OperatorRole.Admin)]
    [InlineData(OperatorRole.Owner)]
    public async Task ActivateAsync_AdminActivatingSameOrHigherRole_ShouldThrowForbiddenException(OperatorRole targetRole)
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));
        _repo.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(MakeUser(targetRole, targetId));

        var act = () => _service.ActivateAsync(adminId, targetId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ActivateAsync_AdminActivatingOperator_ShouldSucceed()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var target = OperatorUser.Reconstitute(targetId, "User", "user@neo.com",
            PasswordHash.FromHash("hash"), OperatorRole.Operator, false, DateTime.UtcNow);
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));
        _repo.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(target);

        await _service.ActivateAsync(adminId, targetId);

        _repo.Verify(r => r.UpdateAsync(It.Is<OperatorUser>(u => u.IsActive)), Times.Once);
    }

    // ── login (TD-105 e TD-106) ──────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_UnknownEmail_WrongPassword_AndInactive_ShouldThrowTheSameException()
    {
        var active = MakeUser(OperatorRole.Operator, Guid.NewGuid());
        var inactive = OperatorUser.Reconstitute(Guid.NewGuid(), "User", "inactive@neo.com",
            PasswordHash.FromHash("hash"), OperatorRole.Operator, false, DateTime.UtcNow);
        _repo.Setup(r => r.GetByEmailAsync("unknown@neo.com")).ReturnsAsync((OperatorUser?)null);
        _repo.Setup(r => r.GetByEmailAsync("active@neo.com")).ReturnsAsync(active);
        _repo.Setup(r => r.GetByEmailAsync("inactive@neo.com")).ReturnsAsync(inactive);
        _hasher.Setup(h => h.Verify("right", "hash")).Returns(true);

        var cases = new[]
        {
            new LoginRequest("unknown@neo.com", "right"),
            new LoginRequest("active@neo.com", "wrong"),
            new LoginRequest("inactive@neo.com", "right"),
        };

        foreach (var request in cases)
        {
            var act = () => _service.LoginAsync(request);
            (await act.Should().ThrowAsync<InvalidCredentialsException>()).Which.Message
                .Should().Be("Invalid credentials.");
        }
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ShouldStillRunTheHasher()
    {
        _repo.Setup(r => r.GetByEmailAsync("unknown@neo.com")).ReturnsAsync((OperatorUser?)null);

        var act = () => _service.LoginAsync(new LoginRequest("unknown@neo.com", "any-password"));

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _hasher.Verify(h => h.Verify("any-password", OperatorService.UnknownEmailHash), Times.Once);
    }

    [Fact]
    public void UnknownEmailHash_ShouldHaveTheCostOfARealHash()
    {
        var hasher = new BcryptPasswordHasher();

        OperatorService.UnknownEmailHash[..7].Should().Be(hasher.Hash("any-password")[..7]);
        hasher.Verify("any-password", OperatorService.UnknownEmailHash).Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnTheOperatorData()
    {
        var user = MakeUser(OperatorRole.Admin, Guid.NewGuid());
        _repo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("right", user.PasswordHash.Value)).Returns(true);

        var result = await _service.LoginAsync(new LoginRequest(user.Email, "right"));

        result.Should().Be(new OperatorResponseDTO(user.Id, user.Name, user.Email, "Admin", user.IsActive));
    }
}
