namespace NeoParking.Management.Tests.Unit.Application;

using FluentAssertions;
using Moq;
using NeoParking.Management.Application;
using NeoParking.Management.Domain;
using NeoParking.Management.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;

public class OperatorServiceTests
{
    private readonly Mock<IOperatorRepository> _repo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly OperatorService _service;

    public OperatorServiceTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _service = new OperatorService(_repo.Object, _hasher.Object);
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
    public async Task CreateOperatorAsync_AdminCreatingAdmin_ShouldThrowDomainException()
    {
        var adminId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((OperatorUser?)null);

        var act = async () => await _service.CreateOperatorAsync(adminId,
            new CreateOperatorRequest("Op", "op@neo.com", "pass", "Admin"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Only the Owner*");
    }

    [Fact]
    public async Task CreateOperatorAsync_ByRegularOperator_ShouldThrowDomainException()
    {
        var opId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(opId)).ReturnsAsync(MakeUser(OperatorRole.Operator, opId));

        var act = async () => await _service.CreateOperatorAsync(opId,
            new CreateOperatorRequest("Op", "op@neo.com", "pass", "Operator"));

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Insufficient permissions*");
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
    public async Task PromoteToAdminAsync_ByAdmin_ShouldThrowDomainException()
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));
        _repo.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(MakeUser(OperatorRole.Operator, targetId));

        var act = async () => await _service.PromoteToAdminAsync(adminId, targetId);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Only the Owner*");
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
    public async Task DeactivateAsync_OperatorDeactivatingAdmin_ShouldThrowDomainException()
    {
        var opId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(opId)).ReturnsAsync(MakeUser(OperatorRole.Operator, opId));
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));

        var act = async () => await _service.DeactivateAsync(opId, adminId);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Insufficient permissions*");
    }

    [Fact]
    public async Task DeactivateAsync_AdminDeactivatingAdmin_ShouldThrowDomainException()
    {
        var adminId = Guid.NewGuid();
        var targetAdminId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, adminId));
        _repo.Setup(r => r.GetByIdAsync(targetAdminId)).ReturnsAsync(MakeUser(OperatorRole.Admin, targetAdminId));

        var act = async () => await _service.DeactivateAsync(adminId, targetAdminId);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*same or higher role*");
    }

    // ── not found ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ShouldThrowNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((OperatorUser?)null);

        var act = async () => await _service.GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
