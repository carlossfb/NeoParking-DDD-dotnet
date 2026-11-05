namespace NeoParking.Management.Tests.Unit.Application;

using FluentAssertions;
using Moq;
using NeoParking.Management.Application;
using NeoParking.Management.Domain;
using NeoParking.Management.Domain.Vo;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Primitives;

public class OperatorListTests
{
    private readonly Mock<IOperatorRepository> _repo = new();
    private readonly OperatorService _service;

    public OperatorListTests() =>
        _service = new OperatorService(_repo.Object, Mock.Of<IPasswordHasher>(), Mock.Of<IUnitOfWork>());

    private OperatorUser Requester(OperatorRole role)
    {
        var user = OperatorUser.Create("User", "user@neo.com", PasswordHash.FromHash("hash"), role);
        _repo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        return user;
    }

    [Theory]
    [InlineData(OperatorRole.Admin)]
    [InlineData(OperatorRole.Owner)]
    public async Task ListAsync_ForOwnerOrAdmin_ShouldMapThePage(OperatorRole role)
    {
        var requester = Requester(role);
        var page = new PageRequest(1, 10);
        _repo.Setup(r => r.ListAsync(page)).ReturnsAsync(new PagedResult<OperatorUser>([requester], 1, 10, 1));

        var result = await _service.ListAsync(requester.Id, page);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.Id == requester.Id && i.Email == "user@neo.com");
    }

    [Fact]
    public async Task ListAsync_ForOperator_ShouldBeForbidden()
    {
        var requester = Requester(OperatorRole.Operator);

        var act = () => _service.ListAsync(requester.Id, new PageRequest());

        await act.Should().ThrowAsync<ForbiddenException>();
        _repo.Verify(r => r.ListAsync(It.IsAny<PageRequest>()), Times.Never);
    }
}
