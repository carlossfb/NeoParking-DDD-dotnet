namespace NeoParking.Management.Tests.Unit.Application;

using FluentAssertions;
using Moq;
using NeoParking.Management.Application;
using NeoParking.Management.Domain;
using NeoParking.Management.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;

public class ChangePasswordTests
{
    private readonly Mock<IOperatorRepository> _repo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OperatorService _service;
    private readonly OperatorUser _user = OperatorUser.Create("User", "user@neo.com", PasswordHash.FromHash("old-hash"), OperatorRole.Operator);

    public ChangePasswordTests()
    {
        _repo.Setup(r => r.GetByIdAsync(_user.Id)).ReturnsAsync(_user);
        _hasher.Setup(h => h.Verify("current", "old-hash")).Returns(true);
        _hasher.Setup(h => h.Hash("new")).Returns("new-hash");
        _service = new OperatorService(_repo.Object, _hasher.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task Change_WithTheCurrentPassword_ShouldSaveTheNewHash()
    {
        var result = await _service.ChangePasswordAsync(_user.Id, new UpdatePasswordRequest("current", "new"));

        result.Should().Be(ChangePasswordResult.Changed);
        _user.PasswordHash.Value.Should().Be("new-hash");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("wrong", "new", ChangePasswordResult.WrongCurrentPassword)]
    [InlineData(null, "new", ChangePasswordResult.WrongCurrentPassword)]
    [InlineData("current", " ", ChangePasswordResult.InvalidNewPassword)]
    [InlineData("current", null, ChangePasswordResult.InvalidNewPassword)]
    public async Task Change_WhenRejected_ShouldKeepTheHash(string? current, string? replacement, ChangePasswordResult expected)
    {
        var result = await _service.ChangePasswordAsync(_user.Id, new UpdatePasswordRequest(current!, replacement!));

        result.Should().Be(expected);
        _user.PasswordHash.Value.Should().Be("old-hash");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Change_ByAnInactiveAccount_ShouldBeForbidden()
    {
        var owner = _user;
        owner.Deactivate();

        var act = () => _service.ChangePasswordAsync(owner.Id, new UpdatePasswordRequest("current", "new"));

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
